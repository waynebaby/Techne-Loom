(function (root, createApi) {
    const api = createApi();
    root.AskUserAnswerValidator = api;
    if (typeof module !== "undefined" && module.exports) {
        module.exports = api;
    }
}(globalThis, function () {
    "use strict";

    function validate(contract, answers, attachments, requireComplete) {
        const diagnostics = [];
        const normalizedAnswers = [];
        const knownQuestions = new Map();
        const answerMap = answers && typeof answers === "object" ? answers : {};
        const attachmentMap = createAttachmentMap(attachments);
        const complete = requireComplete !== false;
        const groups = Array.isArray(contract && contract.questionGroups) ? contract.questionGroups : [];

        for (const group of groups) {
            if (!group || !Array.isArray(group.questions)) {
                continue;
            }

            for (const question of group.questions) {
                if (!question || typeof question.id !== "string" || question.id.length === 0) {
                    continue;
                }

                knownQuestions.set(question.id, question);
                const location = `question:${question.id}`;
                const submittedAnswer = Object.prototype.hasOwnProperty.call(answerMap, question.id) ? answerMap[question.id] : undefined;
                if (!submittedAnswer || typeof submittedAnswer !== "object" || Array.isArray(submittedAnswer)) {
                    if (complete) {
                        add(diagnostics, location, question.required
                            ? "A required question must be answered."
                            : "An optional question must be explicitly skipped or answered.");
                    }
                    continue;
                }

                const answer = {
                    value: submittedAnswer.value === undefined ? null : submittedAnswer.value,
                    skipped: submittedAnswer.skipped === undefined ? false : submittedAnswer.skipped,
                    attachmentIds: submittedAnswer.attachmentIds === undefined ? [] : submittedAnswer.attachmentIds
                };
                if (typeof answer.skipped !== "boolean") {
                    add(diagnostics, location, "The skipped flag must be a boolean.");
                    continue;
                }
                if (!Array.isArray(answer.attachmentIds)) {
                    add(diagnostics, location, "Attachment ids cannot be null.");
                    continue;
                }

                if (answer.skipped) {
                    if (question.required) {
                        add(diagnostics, location, "A required question cannot be skipped.");
                        continue;
                    }
                    if (answer.value !== undefined && answer.value !== null || answer.attachmentIds.length > 0) {
                        add(diagnostics, location, "A skipped question cannot also contain an answer or attachment.");
                        continue;
                    }
                    normalizedAnswers.push({ questionId: question.id, contextPath: question.contextPath, value: null, skipped: true, attachments: [] });
                    continue;
                }

                const selectedAttachments = resolveAttachments(question, answer, attachmentMap, location, diagnostics);
                const valid = validateValue(question, answer, selectedAttachments, location, diagnostics);
                if (valid) {
                    normalizedAnswers.push({
                        questionId: question.id,
                        contextPath: question.contextPath,
                        value: answer.value === undefined ? null : answer.value,
                        skipped: false,
                        attachments: selectedAttachments
                    });
                }
            }
        }

        for (const questionId of Object.keys(answerMap)) {
            if (!knownQuestions.has(questionId)) {
                add(diagnostics, `question:${questionId}`, "The answer references an unknown question id.");
            }
        }

        return { valid: diagnostics.length === 0, diagnostics, normalizedAnswers };
    }

    function validateValue(question, answer, attachments, location, diagnostics) {
        const choices = Array.isArray(question.options) ? question.options.map(option => option && option.value).filter(value => typeof value === "string") : [];
        const constraints = question.constraints || {};
        const hasValue = Object.prototype.hasOwnProperty.call(answer, "value") && answer.value !== null;
        const noAttachments = answer.attachmentIds.length === 0;
        const value = answer.value;

        switch (question.type) {
            case "singleChoice":
                if (!noAttachments || !hasValue || typeof value !== "string") {
                    return fail(diagnostics, location, "A single-choice answer must be one option value string.");
                }
                if (!choices.includes(value)) {
                    return fail(diagnostics, location, "The selected value is not a declared option.");
                }
                return true;
            case "multipleChoice": {
                if (!noAttachments || !Array.isArray(value)) {
                    return fail(diagnostics, location, "A multiple-choice answer must be an array of option value strings.");
                }
                if (value.some(item => typeof item !== "string")) {
                    return fail(diagnostics, location, "A multiple-choice answer must contain only option value strings.");
                }
                if (new Set(value).size !== value.length) {
                    return fail(diagnostics, location, "A multiple-choice answer cannot repeat an option value.");
                }
                if (value.some(item => !choices.includes(item))) {
                    return fail(diagnostics, location, "A multiple-choice answer contains an undeclared option value.");
                }
                if (!withinBounds(value.length, constraints.minSelections, constraints.maxSelections)) {
                    return fail(diagnostics, location, "The number of selected options is outside the configured bounds.");
                }
                return true;
            }
            case "text":
                if (!noAttachments || !hasValue || typeof value !== "string") {
                    return fail(diagnostics, location, "A text answer must be a string.");
                }
                if (!withinBounds(value.length, constraints.minLength, constraints.maxLength)) {
                    return fail(diagnostics, location, "The text answer is outside the configured length bounds.");
                }
                return true;
            case "number":
                if (!noAttachments || !hasValue || typeof value !== "number" || !Number.isFinite(value)) {
                    return fail(diagnostics, location, "A number answer must be a finite JSON number.");
                }
                if (constraints.minimum !== undefined && value < constraints.minimum
                    || constraints.maximum !== undefined && value > constraints.maximum) {
                    return fail(diagnostics, location, "The number answer is outside the configured bounds.");
                }
                return true;
            case "boolean":
                if (!noAttachments || !hasValue || typeof value !== "boolean") {
                    return fail(diagnostics, location, "A boolean answer must be true or false.");
                }
                return true;
            case "file":
                return validateAttachment(question, answer, attachments, location, diagnostics, false);
            case "audio":
                return validateAttachment(question, answer, attachments, location, diagnostics, true);
            default:
                return fail(diagnostics, location, `Answer type '${question.type}' is not supported.`);
        }
    }

    function validateAttachment(question, answer, attachments, location, diagnostics, audio) {
        if (answer.value !== undefined && answer.value !== null || attachments.length !== 1) {
            return fail(diagnostics, location, audio
                ? "An audio answer must reference exactly one uploaded audio attachment."
                : "A file answer must reference exactly one uploaded attachment.");
        }

        const attachment = attachments[0];
        if (!attachment || !Number.isSafeInteger(attachment.length) || attachment.length <= 0
            || typeof attachment.sha256 !== "string" || !/^[0-9a-f]{64}$/i.test(attachment.sha256)
            || typeof attachment.mediaType !== "string" || !parseMediaType(attachment.mediaType)) {
            return fail(diagnostics, location, "The uploaded attachment metadata is invalid.");
        }
        if (constraintsMaximum(question) !== null && attachment.length > constraintsMaximum(question)) {
            return fail(diagnostics, location, "The uploaded attachment exceeds the configured size limit.");
        }
        if (audio && !matchesMediaType(attachment.mediaType, "audio/*")) {
            return fail(diagnostics, location, "An audio question accepts only audio media types.");
        }
        const allowed = question.constraints && Array.isArray(question.constraints.allowedMediaTypes)
            ? question.constraints.allowedMediaTypes
            : [];
        if (allowed.length > 0 && !allowed.some(pattern => matchesMediaType(attachment.mediaType, pattern))) {
            return fail(diagnostics, location, "The uploaded attachment media type is not allowed.");
        }
        return true;
    }

    function constraintsMaximum(question) {
        const maximum = question.constraints && question.constraints.maxAttachmentBytes;
        return typeof maximum === "number" ? maximum : null;
    }

    function resolveAttachments(question, answer, attachmentMap, location, diagnostics) {
        const resolved = [];
        if (new Set(answer.attachmentIds).size !== answer.attachmentIds.length) {
            add(diagnostics, location, "An answer cannot reference an attachment more than once.");
            return resolved;
        }
        for (const attachmentId of answer.attachmentIds) {
            const metadata = attachmentMap.get(attachmentId);
            if (!metadata) {
                add(diagnostics, location, `Attachment '${attachmentId}' is not part of this ask.`);
                continue;
            }
            if (metadata.questionId !== question.id) {
                add(diagnostics, location, "An attachment can only be used by its designated question.");
                continue;
            }
            resolved.push(metadata);
        }
        return resolved;
    }

    function createAttachmentMap(attachments) {
        const map = new Map();
        const entries = Array.isArray(attachments)
            ? attachments.map(item => [item && item.attachmentId, item])
            : Object.entries(attachments || {});
        for (const [key, metadata] of entries) {
            if (typeof key === "string" && key.length > 0) {
                map.set(key, metadata);
            }
        }
        return map;
    }

    function parseMediaType(value) {
        const mediaType = value.split(";", 1)[0].trim();
        const parts = mediaType.split("/");
        return parts.length === 2 && parts.every(part => /^[A-Za-z0-9!#$&^_.+-]+$/.test(part)) ? parts : null;
    }

    function matchesMediaType(mediaType, pattern) {
        if (typeof pattern !== "string") {
            return false;
        }
        const mediaParts = parseMediaType(mediaType);
        const patternParts = parseMediaType(pattern.endsWith("/*") ? `${pattern.slice(0, -1)}x` : pattern);
        if (!mediaParts || !patternParts) {
            return false;
        }
        return (patternParts[0] === "*" || patternParts[0].toLowerCase() === mediaParts[0].toLowerCase())
            && (pattern.endsWith("/*") || patternParts[1].toLowerCase() === mediaParts[1].toLowerCase());
    }

    function withinBounds(value, minimum, maximum) {
        return (minimum === undefined || minimum === null || value >= minimum)
            && (maximum === undefined || maximum === null || value <= maximum);
    }

    function fail(diagnostics, location, message) {
        add(diagnostics, location, message);
        return false;
    }

    function add(diagnostics, location, message) {
        diagnostics.push({ location, message });
    }

    function withDefaults(contract, answers) {
        const seededAnswers = { ...(answers || {}) };
        for (const group of contract && Array.isArray(contract.questionGroups) ? contract.questionGroups : []) {
            for (const question of group && Array.isArray(group.questions) ? group.questions : []) {
                if (!question || typeof question.id !== "string" || Object.prototype.hasOwnProperty.call(seededAnswers, question.id)) {
                    continue;
                }
                if (question.defaultValue === undefined || question.defaultValue === null) {
                    continue;
                }
                seededAnswers[question.id] = {
                    value: question.defaultValue,
                    skipped: false,
                    attachmentIds: []
                };
            }
        }
        return seededAnswers;
    }

    return { validate, withDefaults };
}));
