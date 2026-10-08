const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const test = require("node:test");
const vm = require("node:vm");

const assetsDirectory = path.join(__dirname, "../../src/dotnet/Techne.Loom.Common/TaskTracking/Runtime/AskUserAssets");
const appSource = fs.readFileSync(path.join(assetsDirectory, "app.js"), "utf8");
const validatorSource = fs.readFileSync(path.join(assetsDirectory, "answer-validator.js"), "utf8");

class FakeElement {
    constructor(document, tagName) {
        this.document = document;
        this.tagName = tagName.toLowerCase();
        this.children = [];
        this.attributes = new Map();
        this.listeners = new Map();
        this.dataset = {};
        this.style = {};
        this._textContent = "";
        this.parentNode = null;
    }

    get textContent() {
        return this._textContent + this.children.map(child => child.textContent).join("");
    }

    set textContent(value) {
        this._textContent = value === undefined || value === null ? "" : String(value);
        this.children = [];
    }

    append(...children) {
        for (const child of children) {
            child.parentNode = this;
            this.children.push(child);
        }
    }

    replaceChildren(...children) {
        this._textContent = "";
        this.children = [];
        this.append(...children);
    }

    setAttribute(name, value) {
        this.attributes.set(name, String(value));
    }

    removeAttribute(name) {
        this.attributes.delete(name);
    }

    addEventListener(name, listener) {
        const listeners = this.listeners.get(name) || [];
        listeners.push(listener);
        this.listeners.set(name, listeners);
    }

    async dispatch(name, event = {}) {
        for (const listener of this.listeners.get(name) || []) {
            await listener({ preventDefault() {}, target: this, ...event });
        }
    }

    click() {
        if (this.tagName === "a" && this.download) {
            this.document.downloads.push({
                fileName: this.download,
                blob: this.document.objectUrls.get(this.href)
            });
        }
        for (const listener of this.listeners.get("click") || []) {
            listener({ preventDefault() {}, target: this });
        }
    }

    remove() {
        if (this.parentNode) {
            this.parentNode.children = this.parentNode.children.filter(child => child !== this);
            this.parentNode = null;
        }
    }
}

class FakeDocument {
    constructor() {
        this.app = new FakeElement(this, "div");
        this.app.id = "app";
        this.body = new FakeElement(this, "body");
        this.downloads = [];
        this.objectUrls = new Map();
        this.visibilityState = "visible";
    }

    getElementById(id) {
        if (id === "app") {
            return this.app;
        }
        return this.findAll(this.app, element => element.id === id)[0] || null;
    }

    createElement(tagName) {
        return new FakeElement(this, tagName);
    }

    findAll(root, predicate) {
        const results = [];
        for (const child of root.children) {
            if (predicate(child)) {
                results.push(child);
            }
            results.push(...this.findAll(child, predicate));
        }
        return results;
    }

    findButtons(label) {
        return this.findAll(this.app, element => element.tagName === "button" && element.textContent === label);
    }
}

class MemoryStorage {
    constructor() {
        this.values = new Map();
    }

    getItem(key) {
        return this.values.has(key) ? this.values.get(key) : null;
    }

    setItem(key, value) {
        this.values.set(key, String(value));
    }

    removeItem(key) {
        this.values.delete(key);
    }
}

function createBrowser({ hash = "", storage = new MemoryStorage(), offlineBootstrap = null, fetch }) {
    const clipboardWrites = [];
    const document = new FakeDocument();
    const location = { hash, protocol: offlineBootstrap ? "file:" : "http:", pathname: "/", search: "", origin: "http://127.0.0.1:43123" };
    const context = {
        document,
        location,
        navigator: {
            clipboard: {
                writeText: async value => clipboardWrites.push(value)
            }
        },
        sessionStorage: storage,
        AskUserOfflineBootstrap: offlineBootstrap,
        fetch,
        Headers,
        URLSearchParams,
        Blob,
        crypto: { randomUUID: () => "offline-operation-1" },
        console,
        setTimeout: () => 1,
        clearTimeout: () => {},
        setInterval: () => 1,
        addEventListener: () => {},
        objectUrlCounter: 0
    };
    context.window = context;
    context.history = {
        replaceState(_state, _title, url) {
            const hashIndex = String(url).indexOf("#");
            location.hash = hashIndex < 0 ? "" : String(url).slice(hashIndex);
        }
    };
    context.URL = {
        createObjectURL(blob) {
            const url = `blob:test-${++context.objectUrlCounter}`;
            document.objectUrls.set(url, blob);
            return url;
        },
        revokeObjectURL(url) {
            document.objectUrls.delete(url);
        }
    };
    const vmContext = vm.createContext(context);
    vm.runInContext(validatorSource, vmContext, { filename: "answer-validator.js" });
    vm.runInContext(appSource, vmContext, { filename: "app.js" });
    return { document, location, storage, clipboardWrites };
}

async function flushInitialization() {
    for (let index = 0; index < 8; index += 1) {
        await new Promise(resolve => setImmediate(resolve));
    }
}

function jsonResponse(value) {
    return {
        status: 200,
        ok: true,
        headers: new Headers({ "Content-Type": "application/json" }),
        json: async () => value,
        text: async () => JSON.stringify(value)
    };
}

test("reuses the tab-scoped session after the one-use pairing fragment is consumed", async () => {
    const storage = new MemoryStorage();
    const calls = [];
    const fetch = async (url, options = {}) => {
        calls.push({ url, options });
        if (url === "/api/session") {
            assert.equal(JSON.parse(options.body).pairingCode, "one-use-code");
            return jsonResponse({ token: "tab-session-token" });
        }
        if (url === "/api/ask") {
            assert.equal(options.headers.get("Authorization"), "Bearer tab-session-token");
            return jsonResponse({
                askId: "ask-refresh",
                generation: 1,
                contract: { questionGroups: [{ id: "identity", title: "Identity", questions: [{ id: "name", prompt: "Name?", contextPath: "name", type: "text", required: true }] }] },
                draftAnswers: {},
                attachments: [],
                receipt: null
            });
        }
        throw new Error(`Unexpected request ${url}`);
    };

    const firstLoad = createBrowser({ hash: "#pair=one-use-code", storage, fetch });
    await flushInitialization();
    assert.equal(firstLoad.location.hash, "");
    assert.equal(calls.filter(call => call.url === "/api/session").length, 1);

    const refreshed = createBrowser({ storage, fetch });
    await flushInitialization();
    assert.equal(calls.filter(call => call.url === "/api/session").length, 1);
    assert.equal(calls.filter(call => call.url === "/api/ask").length, 2);
    assert.ok(refreshed.document.findAll(refreshed.document.app, element => element.textContent === "Connected to this ask").length > 0);
});

test("downloads a versioned offline submission and disables unavailable attachment downloads", async () => {
    const attachment = {
        attachmentId: "attachment-1",
        questionId: "document",
        fileName: "notes.txt",
        mediaType: "text/plain",
        length: 5,
        sha256: "a".repeat(64)
    };
    const browser = createBrowser({
        offlineBootstrap: {
            schemaVersion: 1,
            askId: "ask-offline",
            generation: 4,
            contract: {
                questionGroups: [{
                    id: "files",
                    title: "Files",
                    questions: [{
                        id: "document",
                        prompt: "Attach a document",
                        contextPath: "document",
                        type: "file",
                        required: true,
                        constraints: { allowedMediaTypes: ["text/*"], maxAttachmentBytes: 100 }
                    }]
                }]
            },
            draftAnswers: { document: { value: null, skipped: false, attachmentIds: [attachment.attachmentId] } },
            attachments: [attachment]
        },
        fetch: async () => { throw new Error("Offline copies must not make network requests."); }
    });
    await flushInitialization();

    const unavailableDownload = browser.document.findButtons("Unavailable offline")[0];
    assert.ok(unavailableDownload);
    assert.equal(unavailableDownload.disabled, true);

    const downloadAnswers = browser.document.findButtons("Download answers")[0];
    assert.ok(downloadAnswers);
    downloadAnswers.click();
    await flushInitialization();
    assert.equal(browser.document.downloads.length, 1);

    const submission = JSON.parse(await browser.document.downloads[0].blob.text());
    assert.equal(submission.schemaVersion, 1);
    assert.equal(submission.askId, "ask-offline");
    assert.equal(submission.expectedGeneration, 4);
    assert.equal(submission.operationId, "offline-operation-1");
    assert.deepEqual(submission.answers.document.attachmentIds, [attachment.attachmentId]);
});

test("a new pairing link takes precedence over a prior tab session", async () => {
    const storage = new MemoryStorage();
    storage.setItem("techneLoomAskSessionToken", "old-session-token");
    const calls = [];
    const fetch = async (url, options = {}) => {
        calls.push({ url, options });
        if (url === "/api/session") {
            assert.equal(JSON.parse(options.body).pairingCode, "new-one-use-code");
            return jsonResponse({ token: "new-session-token" });
        }
        if (url === "/api/ask") {
            assert.equal(options.headers.get("Authorization"), "Bearer new-session-token");
            return jsonResponse({
                askId: "new-ask",
                generation: 1,
                contract: { questionGroups: [] },
                draftAnswers: {},
                attachments: [],
                receipt: null
            });
        }
        throw new Error(`Unexpected request ${url}`);
    };

    const browser = createBrowser({ hash: "#pair=new-one-use-code", storage, fetch });
    await flushInitialization();
    assert.equal(calls.filter(call => call.url === "/api/session").length, 1);
    assert.equal(storage.getItem("techneLoomAskSessionToken"), "new-session-token");
    assert.equal(browser.location.hash, "");
});

test("keeps the tab session after a transient refresh failure", async () => {
    const storage = new MemoryStorage();
    storage.setItem("techneLoomAskSessionToken", "tab-session-token");
    const calls = [];
    const fetch = async (url, options = {}) => {
        calls.push({ url, options });
        if (url === "/api/ask") {
            throw new TypeError("The local worker is temporarily unavailable.");
        }
        throw new Error(`Unexpected request ${url}`);
    };

    createBrowser({ storage, fetch });
    await flushInitialization();
    assert.equal(calls.filter(call => call.url === "/api/session").length, 0);
    assert.equal(storage.getItem("techneLoomAskSessionToken"), "tab-session-token");
});

test("clears a rejected tab session without replaying pairing", async () => {
    const storage = new MemoryStorage();
    storage.setItem("techneLoomAskSessionToken", "expired-session-token");
    const calls = [];
    const fetch = async (url, options = {}) => {
        calls.push({ url, options });
        if (url === "/api/ask") {
            assert.equal(options.headers.get("Authorization"), "Bearer expired-session-token");
            return {
                status: 401,
                ok: false,
                headers: new Headers({ "Content-Type": "application/json" }),
                json: async () => ({ error: "A valid ask session is required." }),
                text: async () => "A valid ask session is required."
            };
        }
        throw new Error(`Unexpected request ${url}`);
    };

    const browser = createBrowser({ storage, fetch });
    await flushInitialization();
    assert.equal(storage.getItem("techneLoomAskSessionToken"), null);
    assert.equal(calls.filter(call => call.url === "/api/session").length, 0);
    assert.ok(browser.document.findAll(browser.document.app, element => element.textContent === "Ask unavailable").length > 0);
});

test("copies a cURL submission request accepted by the local worker", async () => {
    const fetch = async url => {
        if (url === "/api/session") {
            return jsonResponse({ token: "curl-session-token" });
        }
        if (url === "/api/ask") {
            return jsonResponse({
                askId: "ask-curl",
                generation: 0,
                contract: {
                    questionGroups: [{
                        id: "details",
                        title: "Details",
                        questions: [{ id: "name", prompt: "Name?", contextPath: "name", type: "text", required: true }]
                    }]
                },
                draftAnswers: {},
                attachments: [],
                receipt: null
            });
        }
        throw new Error(`Unexpected request ${url}`);
    };

    const browser = createBrowser({ hash: "#pair=curl-pairing-code", fetch });
    await flushInitialization();
    browser.document.findButtons("JSON")[0].click();
    const copyCurl = browser.document.findButtons("Copy cURL")[0];
    assert.ok(copyCurl);
    copyCurl.click();
    await flushInitialization();
    assert.equal(browser.clipboardWrites.length, 1);
    assert.match(browser.clipboardWrites[0], /--header 'Origin: http:\/\/127\.0\.0\.1:43123'/);
});

test("saves the active draft before downloading an offline copy", async () => {
    const calls = [];
    const fetch = async (url, options = {}) => {
        if (url === "/api/session") {
            return jsonResponse({ token: "offline-session-token" });
        }
        if (url === "/api/ask") {
            return jsonResponse({
                askId: "ask-offline-copy",
                generation: 0,
                contract: {
                    questionGroups: [{
                        id: "details",
                        title: "Details",
                        questions: [{ id: "question.name", prompt: "Name?", contextPath: "name", type: "text", required: true }]
                    }]
                },
                draftAnswers: {},
                attachments: [],
                receipt: null
            });
        }
        if (url === "/api/draft") {
            calls.push("draft");
            const body = JSON.parse(options.body);
            assert.equal(body.answers["question.name"].value, "Ada");
            return jsonResponse({
                askId: "ask-offline-copy",
                generation: 1,
                contract: {
                    questionGroups: [{
                        id: "details",
                        title: "Details",
                        questions: [{ id: "question.name", prompt: "Name?", contextPath: "name", type: "text", required: true }]
                    }]
                },
                draftAnswers: body.answers,
                attachments: [],
                receipt: null
            });
        }
        if (url === "/api/offline") {
            calls.push("offline");
            return {
                ok: true,
                headers: new Headers({ "Content-Disposition": "attachment; filename=ask-offline-copy.html" }),
                blob: async () => new Blob(["offline-copy"])
            };
        }
        throw new Error(`Unexpected request ${url}`);
    };

    const browser = createBrowser({ hash: "#pair=offline-pairing-code", fetch });
    await flushInitialization();
    const answerInput = browser.document.findAll(browser.document.app, element => element.tagName === "textarea")[0];
    answerInput.value = "Ada";
    await answerInput.dispatch("input");
    browser.document.findButtons("Save offline copy")[0].click();
    await flushInitialization();

    assert.deepEqual(calls, ["draft", "offline"]);
    assert.equal(browser.document.downloads.length, 1);
});

test("saves edits made during draft sync before downloading an offline copy", async () => {
    const calls = [];
    let savedAnswers = {};
    let resolveFirstDraftStarted;
    let releaseFirstDraft;
    const firstDraftStarted = new Promise(resolve => {
        resolveFirstDraftStarted = resolve;
    });
    const fetch = async (url, options = {}) => {
        if (url === "/api/session") {
            return jsonResponse({ token: "offline-race-session-token" });
        }
        if (url === "/api/ask") {
            return jsonResponse({
                askId: "ask-offline-race",
                generation: 0,
                contract: {
                    questionGroups: [{
                        id: "details",
                        title: "Details",
                        questions: [{ id: "question.name", prompt: "Name?", contextPath: "name", type: "text", required: true }]
                    }]
                },
                draftAnswers: {},
                attachments: [],
                receipt: null
            });
        }
        if (url === "/api/draft") {
            const answers = JSON.parse(options.body).answers;
            const draftName = answers["question.name"].value;
            calls.push(`draft:${draftName}`);
            if (calls.length === 1) {
                resolveFirstDraftStarted();
                await new Promise(resolve => {
                    releaseFirstDraft = resolve;
                });
            }
            savedAnswers = answers;
            return jsonResponse({
                askId: "ask-offline-race",
                generation: calls.filter(call => call.startsWith("draft:")).length,
                contract: {
                    questionGroups: [{
                        id: "details",
                        title: "Details",
                        questions: [{ id: "question.name", prompt: "Name?", contextPath: "name", type: "text", required: true }]
                    }]
                },
                draftAnswers: answers,
                attachments: [],
                receipt: null
            });
        }
        if (url === "/api/offline") {
            calls.push("offline");
            return {
                ok: true,
                headers: new Headers({ "Content-Disposition": "attachment; filename=ask-offline-race.html" }),
                blob: async () => new Blob([JSON.stringify(savedAnswers)])
            };
        }
        throw new Error(`Unexpected request ${url}`);
    };

    const browser = createBrowser({ hash: "#pair=offline-race-pairing-code", fetch });
    await flushInitialization();
    const answerInput = browser.document.findAll(browser.document.app, element => element.tagName === "textarea")[0];
    answerInput.value = "Ada";
    await answerInput.dispatch("input");
    browser.document.findButtons("Save offline copy")[0].click();
    await firstDraftStarted;

    answerInput.value = "Grace";
    await answerInput.dispatch("input");
    releaseFirstDraft();
    await flushInitialization();

    assert.deepEqual(calls, ["draft:Ada", "draft:Grace", "offline"]);
    assert.equal(browser.document.downloads.length, 1);
    const offlineAnswers = JSON.parse(await browser.document.downloads[0].blob.text());
    assert.equal(offlineAnswers["question.name"].value, "Grace");
});

test("downloads online answers with the generation from their saved draft", async () => {
    const calls = [];
    const fetch = async (url, options = {}) => {
        if (url === "/api/session") {
            return jsonResponse({ token: "download-session-token" });
        }
        if (url === "/api/ask") {
            return jsonResponse({
                askId: "ask-download-generation",
                generation: 4,
                contract: {
                    questionGroups: [{
                        id: "details",
                        title: "Details",
                        questions: [{ id: "question.name", prompt: "Name?", contextPath: "name", type: "text", required: true }]
                    }]
                },
                draftAnswers: {},
                attachments: [],
                receipt: null
            });
        }
        if (url === "/api/draft") {
            const body = JSON.parse(options.body);
            calls.push(body.answers["question.name"].value);
            return jsonResponse({
                askId: "ask-download-generation",
                generation: 5,
                contract: {
                    questionGroups: [{
                        id: "details",
                        title: "Details",
                        questions: [{ id: "question.name", prompt: "Name?", contextPath: "name", type: "text", required: true }]
                    }]
                },
                draftAnswers: body.answers,
                attachments: [],
                receipt: null
            });
        }
        throw new Error(`Unexpected request ${url}`);
    };

    const browser = createBrowser({ hash: "#pair=download-pairing-code", fetch });
    await flushInitialization();
    const answerInput = browser.document.findAll(browser.document.app, element => element.tagName === "textarea")[0];
    answerInput.value = "Ada";
    await answerInput.dispatch("input");
    browser.document.findButtons("JSON")[0].click();
    browser.document.findButtons("Download JSON")[0].click();
    await flushInitialization();

    assert.deepEqual(calls, ["Ada"]);
    assert.equal(browser.document.downloads.length, 1);
    const submission = JSON.parse(await browser.document.downloads[0].blob.text());
    assert.equal(submission.expectedGeneration, 5);
    assert.equal(submission.answers["question.name"].value, "Ada");
});

test("retries the offline copy when answers change during its download", async () => {
    const calls = [];
    let serverAnswers = {};
    let generation = 0;
    let offlineCount = 0;
    let releaseFirstOffline;
    let signalFirstOffline;
    const firstOfflineStarted = new Promise(resolve => {
        signalFirstOffline = resolve;
    });
    const fetch = async (url, options = {}) => {
        if (url === "/api/session") {
            return jsonResponse({ token: "offline-snapshot-race-token" });
        }
        if (url === "/api/ask") {
            return jsonResponse({
                askId: "ask-offline-snapshot-race",
                generation: 0,
                contract: {
                    questionGroups: [{
                        id: "details",
                        title: "Details",
                        questions: [{ id: "question.name", prompt: "Name?", contextPath: "name", type: "text", required: true }]
                    }]
                },
                draftAnswers: {},
                attachments: [],
                receipt: null
            });
        }
        if (url === "/api/draft") {
            serverAnswers = JSON.parse(options.body).answers;
            generation += 1;
            calls.push(`draft:${serverAnswers["question.name"].value}`);
            return jsonResponse({
                askId: "ask-offline-snapshot-race",
                generation,
                contract: {
                    questionGroups: [{
                        id: "details",
                        title: "Details",
                        questions: [{ id: "question.name", prompt: "Name?", contextPath: "name", type: "text", required: true }]
                    }]
                },
                draftAnswers: serverAnswers,
                attachments: [],
                receipt: null
            });
        }
        if (url === "/api/offline") {
            const snapshot = JSON.parse(JSON.stringify(serverAnswers));
            offlineCount += 1;
            calls.push(`offline:${snapshot["question.name"].value}`);
            if (offlineCount === 1) {
                signalFirstOffline();
                await new Promise(resolve => {
                    releaseFirstOffline = resolve;
                });
            }
            return {
                ok: true,
                headers: new Headers({ "Content-Disposition": "attachment; filename=ask-offline-snapshot-race.html" }),
                blob: async () => new Blob([JSON.stringify(snapshot)])
            };
        }
        throw new Error(`Unexpected request ${url}`);
    };

    const browser = createBrowser({ hash: "#pair=offline-snapshot-race-code", fetch });
    await flushInitialization();
    const answerInput = browser.document.findAll(browser.document.app, element => element.tagName === "textarea")[0];
    answerInput.value = "Ada";
    await answerInput.dispatch("input");
    browser.document.findButtons("Save offline copy")[0].click();
    await firstOfflineStarted;

    answerInput.value = "Grace";
    await answerInput.dispatch("input");
    releaseFirstOffline();
    await flushInitialization();

    assert.deepEqual(calls, ["draft:Ada", "offline:Ada", "draft:Grace", "offline:Grace"]);
    assert.equal(browser.document.downloads.length, 1);
    const downloadedSnapshot = JSON.parse(await browser.document.downloads[0].blob.text());
    assert.equal(downloadedSnapshot["question.name"].value, "Grace");
});

test("waits for an attachment upload before downloading an offline copy", async () => {
    const calls = [];
    let savedAnswers = {};
    let resolveUploadStarted;
    let releaseUpload;
    const uploadStarted = new Promise(resolve => {
        resolveUploadStarted = resolve;
    });
    const attachment = {
        attachmentId: "a".repeat(32),
        questionId: "question.file",
        fileName: "notes.txt",
        mediaType: "text/plain",
        length: 4,
        sha256: "b".repeat(64),
        storedAtUtc: "2026-10-09T00:00:00Z"
    };
    const contract = {
        questionGroups: [{
            id: "details",
            title: "Details",
            questions: [
                { id: "question.name", prompt: "Name?", contextPath: "name", type: "text", required: true },
                { id: "question.file", prompt: "File?", contextPath: "file", type: "file", required: true }
            ]
        }]
    };
    const fetch = async (url, options = {}) => {
        if (url === "/api/session") {
            return jsonResponse({ token: "offline-upload-race-token" });
        }
        if (url === "/api/ask") {
            return jsonResponse({ askId: "ask-offline-upload-race", generation: 0, contract, draftAnswers: {}, attachments: [], receipt: null });
        }
        if (url === "/api/attachments/question.file") {
            calls.push("upload-start");
            resolveUploadStarted();
            await new Promise(resolve => {
                releaseUpload = resolve;
            });
            return jsonResponse({ attachment, generation: 1 });
        }
        if (url === "/api/draft") {
            savedAnswers = JSON.parse(options.body).answers;
            calls.push("draft");
            return jsonResponse({ askId: "ask-offline-upload-race", generation: 2, contract, draftAnswers: savedAnswers, attachments: [attachment], receipt: null });
        }
        if (url === "/api/offline") {
            calls.push("offline");
            return {
                ok: true,
                headers: new Headers({ "Content-Disposition": "attachment; filename=ask-offline-upload-race.html" }),
                blob: async () => new Blob([JSON.stringify(savedAnswers)])
            };
        }
        throw new Error(`Unexpected request ${url}`);
    };

    const browser = createBrowser({ hash: "#pair=offline-upload-race-code", fetch });
    await flushInitialization();
    browser.document.findButtons("Next")[0].click();
    await flushInitialization();
    const fileInput = browser.document.findAll(browser.document.app, element => element.tagName === "input" && element.type === "file")[0];
    fileInput.files = [{ name: "notes.txt", type: "text/plain", size: 4 }];
    await fileInput.dispatch("change");
    await uploadStarted;
    browser.document.findButtons("Save offline copy")[0].click();
    await new Promise(resolve => setImmediate(resolve));
    assert.deepEqual(calls, ["upload-start"]);

    releaseUpload();
    await flushInitialization();

    assert.deepEqual(calls, ["upload-start", "draft", "offline"]);
    assert.equal(browser.document.downloads.length, 1);
    const downloadedAnswers = JSON.parse(await browser.document.downloads[0].blob.text());
    assert.deepEqual(downloadedAnswers["question.file"].attachmentIds, [attachment.attachmentId]);
});
