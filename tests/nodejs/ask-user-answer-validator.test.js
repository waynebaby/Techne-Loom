const assert = require("node:assert/strict");
const test = require("node:test");
const validator = require("../../src/dotnet/Techne.Loom.Common/TaskTracking/Runtime/AskUserAssets/answer-validator.js");

const contract = {
    questionGroups: [{
        id: "inputs",
        title: "Inputs",
        questions: [
            { id: "choice", type: "singleChoice", required: true, options: [{ value: "a" }, { value: "b" }] },
            { id: "many", type: "multipleChoice", required: true, options: [{ value: "x" }, { value: "y" }], constraints: { minSelections: 1, maxSelections: 2 } },
            { id: "note", type: "text", required: false, constraints: { minLength: 2, maxLength: 20 } },
            { id: "count", type: "number", required: true, constraints: { minimum: 1, maximum: 10 } },
            { id: "enabled", type: "boolean", required: true },
            { id: "document", type: "file", required: true, constraints: { allowedMediaTypes: ["text/*"], maxAttachmentBytes: 100 } },
            { id: "voice", type: "audio", required: false, constraints: { allowedMediaTypes: ["audio/*"], maxAttachmentBytes: 100 } }
        ]
    }]
};

const documentAttachment = {
    attachmentId: "att-document",
    questionId: "document",
    fileName: "notes.txt",
    mediaType: "text/plain",
    length: 20,
    sha256: "a".repeat(64)
};

const audioAttachment = {
    attachmentId: "att-audio",
    questionId: "voice",
    fileName: "voice.webm",
    mediaType: "audio/webm",
    length: 24,
    sha256: "b".repeat(64)
};

function completeAnswers() {
    return {
        choice: { value: "a", skipped: false, attachmentIds: [] },
        many: { value: ["x", "y"], skipped: false, attachmentIds: [] },
        note: { value: null, skipped: true, attachmentIds: [] },
        count: { value: 3, skipped: false, attachmentIds: [] },
        enabled: { value: false, skipped: false, attachmentIds: [] },
        document: { value: null, skipped: false, attachmentIds: ["att-document"] },
        voice: { value: null, skipped: false, attachmentIds: ["att-audio"] }
    };
}

test("accepts valid answers across all seven structured question types", () => {
    const result = validator.validate(contract, completeAnswers(), [documentAttachment, audioAttachment]);
    assert.equal(result.valid, true);
    assert.equal(result.normalizedAnswers.length, 7);
    assert.equal(result.normalizedAnswers.find(answer => answer.questionId === "enabled").value, false);
});

test("matches server defaults when answer fields are omitted", () => {
    const result = validator.validate(contract, { choice: { value: "a" } }, [], false);
    assert.equal(result.valid, true);
    assert.equal(result.normalizedAnswers.length, 1);
});

test("seeds declared defaults but preserves saved answers", () => {
    const defaultContract = {
        questionGroups: [{ questions: [{ id: "enabled", type: "boolean", required: true, defaultValue: false }] }]
    };
    const seeded = validator.withDefaults(defaultContract, {});
    assert.deepEqual(seeded, { enabled: { value: false, skipped: false, attachmentIds: [] } });
    assert.equal(validator.validate(defaultContract, seeded).valid, true);
    assert.equal(validator.withDefaults(defaultContract, { enabled: { value: true } }).enabled.value, true);
});

test("rejects a non-boolean skipped flag", () => {
    const result = validator.validate(contract, {
        choice: { value: "a", skipped: "no", attachmentIds: [] }
    }, [], false);
    assert.equal(result.valid, false);
    assert.equal(result.diagnostics[0].message, "The skipped flag must be a boolean.");
});

test("rejects incomplete required questions but permits partial draft validation", () => {
    const partial = { choice: { value: "a", skipped: false, attachmentIds: [] } };
    assert.equal(validator.validate(contract, partial, [], true).valid, false);
    assert.equal(validator.validate(contract, partial, [], false).valid, true);
});

test("rejects invalid values, repeated selections, and non-finite numbers", () => {
    const answers = completeAnswers();
    answers.choice.value = "outside-contract";
    answers.many.value = ["x", "x"];
    answers.count.value = Number.POSITIVE_INFINITY;
    const result = validator.validate(contract, answers, [documentAttachment, audioAttachment]);
    assert.equal(result.valid, false);
    assert.equal(result.diagnostics.length, 3);
});

test("rejects attachments assigned to another question or with invalid metadata", () => {
    const answers = completeAnswers();
    answers.document.attachmentIds = ["att-audio"];
    answers.voice.attachmentIds = ["att-bad"];
    const result = validator.validate(contract, answers, [documentAttachment, audioAttachment, {
        ...audioAttachment,
        attachmentId: "att-bad",
        questionId: "voice",
        mediaType: "audio/webm",
        sha256: null
    }]);
    assert.equal(result.valid, false);
    assert.equal(result.diagnostics.length, 3);
});

test("rejects null attachment ids without throwing", () => {
    const answers = completeAnswers();
    answers.document.attachmentIds = null;
    const result = validator.validate(contract, answers, [documentAttachment, audioAttachment]);
    assert.equal(result.valid, false);
    assert.equal(result.diagnostics[0].message, "Attachment ids cannot be null.");
});

test("applies free text according to question semantics", () => {
    const answers = completeAnswers();
    answers.choice = { value: null, freeText: "not listed", skipped: false, attachmentIds: [] };
    answers.many = { value: ["x"], freeText: "custom option", skipped: false, attachmentIds: [] };
    answers.count = { value: 3, freeText: "measured manually", skipped: false, attachmentIds: [] };
    answers.enabled = { value: null, freeText: "unknown", skipped: false, attachmentIds: [] };
    answers.document = { value: null, freeText: "scanned copy", skipped: false, attachmentIds: ["att-document"] };
    answers.voice = { value: null, freeText: "microphone unavailable", skipped: false, attachmentIds: [] };

    const result = validator.validate(contract, answers, [documentAttachment, audioAttachment]);

    assert.equal(result.valid, true);
    const normalized = Object.fromEntries(result.normalizedAnswers.map(answer => [answer.questionId, answer]));
    assert.equal(normalized.choice.freeText, "not listed");
    assert.deepEqual(normalized.many.value, ["x"]);
    assert.equal(normalized.many.freeText, "custom option");
    assert.equal(normalized.count.value, 3);
    assert.equal(normalized.count.freeText, "measured manually");
    assert.equal(normalized.enabled.value, null);
    assert.equal(normalized.enabled.freeText, "unknown");
    assert.equal(normalized.document.attachments.length, 1);
    assert.equal(normalized.document.freeText, "scanned copy");
    assert.equal(normalized.voice.attachments.length, 0);
    assert.equal(normalized.voice.freeText, "microphone unavailable");
});

test("rejects conflicting single-choice text, excess multiple-choice text, and blanks", () => {
    const answers = completeAnswers();
    answers.choice = { value: "a", freeText: "other", skipped: false, attachmentIds: [] };
    answers.many = { value: ["x", "y"], freeText: "extra option", skipped: false, attachmentIds: [] };
    answers.count = { value: null, freeText: "   ", skipped: false, attachmentIds: [] };

    const result = validator.validate(contract, answers, [documentAttachment, audioAttachment]);

    assert.equal(result.valid, false);
    assert.ok(result.diagnostics.some(item => item.message.includes("cannot combine Other text")));
    assert.ok(result.diagnostics.some(item => item.message.includes("selected options is outside")));
    assert.ok(result.diagnostics.some(item => item.message.includes("finite JSON number")));
});
