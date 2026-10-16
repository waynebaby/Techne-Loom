(function (global) {
    "use strict";

    const app = document.getElementById("app");
    const validator = global.AskUserAnswerValidator;
    let offlineBootstrap = global.AskUserOfflineBootstrap || null;
    if (!offlineBootstrap) {
        const bootstrapElement = document.getElementById("ask-user-bootstrap");
        if (bootstrapElement) {
            try {
                offlineBootstrap = JSON.parse(bootstrapElement.textContent || "{}");
            } catch {
                offlineBootstrap = {};
            }
        }
    }
    const state = {
        offline: Boolean(offlineBootstrap) || global.location.protocol === "file:",
        token: null,
        snapshot: null,
        answers: {},
        questions: [],
        currentIndex: 0,
        mode: "guided",
        dirty: false,
        recording: null,
        revision: 0,
        pendingUploads: new Set(),
        saveQueue: Promise.resolve()
    };
    let saveTimer = null;

    function node(tagName, className, text) {
        const element = document.createElement(tagName);
        if (className) {
            element.className = className;
        }
        if (text !== undefined && text !== null) {
            element.textContent = String(text);
        }
        return element;
    }

    function button(label, className, onClick) {
        const element = node("button", className || "button", label);
        element.type = "button";
        element.addEventListener("click", onClick);
        return element;
    }

    function cloneJson(value) {
        return value === undefined ? undefined : JSON.parse(JSON.stringify(value));
    }

    function answerFor(question) {
        const answer = state.answers[question.id];
        if (answer) {
            return {
                value: answer.value === undefined ? null : answer.value,
                skipped: answer.skipped === undefined ? false : answer.skipped,
                attachmentIds: Array.isArray(answer.attachmentIds) ? answer.attachmentIds : [],
                freeText: typeof answer.freeText === "string" ? answer.freeText : null
            };
        }
        if (question.defaultValue !== undefined && question.defaultValue !== null) {
            return { value: question.defaultValue, skipped: false, attachmentIds: [], freeText: null };
        }
        return { value: null, skipped: false, attachmentIds: [], freeText: null };
    }

    function setAnswer(question, value, skipped, freeText) {
        const existing = answerFor(question);
        state.answers[question.id] = {
            value: skipped ? null : value,
            skipped: Boolean(skipped),
            attachmentIds: skipped ? [] : existing.attachmentIds.slice(),
            freeText: skipped ? null : freeText === undefined ? existing.freeText : freeText
        };
        markDirty();
    }

    function markDirty() {
        state.revision += 1;
        state.dirty = true;
        if (!state.offline) {
            window.clearTimeout(saveTimer);
            saveTimer = window.setTimeout(() => {
                void persistDraft().catch(showError);
            }, 500);
        }
        updateSaveStatus(state.offline ? "Offline copy" : "Unsaved changes");
    }

    function updateSaveStatus(message) {
        const status = document.getElementById("save-status");
        if (status) {
            status.textContent = message;
        }
    }

    function updateConnectionStatus(message, kind) {
        const status = document.getElementById("connection-status");
        if (!status) {
            return;
        }
        status.textContent = message;
        status.dataset.kind = kind || "ready";
    }

    function showError(error) {
        const panel = document.getElementById("notice");
        if (!panel) {
            return;
        }
        panel.replaceChildren();
        panel.className = "notice notice-error";
        panel.setAttribute("role", "alert");
        panel.textContent = error && error.message ? error.message : "The request could not be completed.";
    }

    function clearError() {
        const panel = document.getElementById("notice");
        if (panel) {
            panel.replaceChildren();
            panel.className = "notice";
            panel.removeAttribute("role");
        }
    }

    async function request(path, options) {
        const requestOptions = options || {};
        const headers = new Headers(requestOptions.headers || {});
        if (state.token) {
            headers.set("Authorization", `Bearer ${state.token}`);
        }
        if (requestOptions.body !== undefined && !headers.has("Content-Type")) {
            headers.set("Content-Type", "application/json");
        }
        const response = await fetch(path, {
            method: requestOptions.method || "GET",
            headers,
            body: requestOptions.body === undefined ? undefined : JSON.stringify(requestOptions.body),
            cache: "no-store",
            credentials: "omit",
            referrerPolicy: "no-referrer"
        });
        if (response.status === 204) {
            return null;
        }
        const contentType = response.headers.get("Content-Type") || "";
        const body = contentType.includes("application/json")
            ? await response.json()
            : await response.text();
        if (!response.ok) {
            const messages = body && Array.isArray(body.diagnostics)
                ? body.diagnostics.map(item => `${item.location}: ${item.message}`).join("\n")
                : body && body.error;
            const error = new Error(messages || `The request failed with HTTP ${response.status}.`);
            error.status = response.status;
            throw error;
        }
        return body;
    }

    function takePairingCode() {
        const fragment = new URLSearchParams(global.location.hash.slice(1));
        return fragment.get("pair");
    }

    async function initialize() {
        const sessionTokenKey = "techneLoomAskSessionToken";
        try {
            if (state.offline) {
                initializeOffline();
            } else {
                const pairingCode = takePairingCode();
                if (!pairingCode) {
                    const storedToken = global.sessionStorage.getItem(sessionTokenKey);
                    if (storedToken) {
                        state.token = storedToken;
                        try {
                            state.snapshot = await request("/api/ask");
                        } catch (error) {
                            if (!error || error.status !== 401) {
                                throw error;
                            }
                            state.token = null;
                            global.sessionStorage.removeItem(sessionTokenKey);
                        }
                    }
                }
                if (!state.snapshot) {
                    if (!pairingCode) {
                        throw new Error("This ask link is missing its one-use pairing code. Open the complete link from the workflow output.");
                    }
                    const session = await request("/api/session", {
                        method: "POST",
                        body: { pairingCode }
                    });
                    state.token = session.token;
                    global.sessionStorage.setItem(sessionTokenKey, state.token);
                    global.history.replaceState(null, "", global.location.pathname + global.location.search);
                    state.snapshot = await request("/api/ask");
                }
                state.answers = validator.withDefaults(state.snapshot.contract, hydrateAnswers(state.snapshot.draftAnswers));
                state.questions = flattenQuestions(state.snapshot.contract);
                state.currentIndex = firstUnansweredIndex();
                updateConnectionStatus("Connected to this ask", "ready");
            }
            render();
            if (!state.offline) {
                window.setInterval(() => {
                    if (document.visibilityState === "visible") {
                        void refreshStatus();
                    }
                }, 10000);
                window.addEventListener("online", () => void refreshStatus());
            }
        } catch (error) {
            showError(error);
            renderUnavailable();
        }
    }

    function initializeOffline() {
        if (!offlineBootstrap || !offlineBootstrap.contract) {
            throw new Error("The offline ask file is incomplete or unsupported.");
        }
        state.snapshot = {
            askId: offlineBootstrap.askId || "offline",
            generation: Number.isSafeInteger(offlineBootstrap.generation) ? offlineBootstrap.generation : 0,
            contract: offlineBootstrap.contract,
            draftAnswers: offlineBootstrap.draftAnswers || {},
            attachments: Array.isArray(offlineBootstrap.attachments) ? offlineBootstrap.attachments : [],
            receipt: null
        };
                state.answers = validator.withDefaults(state.snapshot.contract, hydrateAnswers(state.snapshot.draftAnswers));
        state.questions = flattenQuestions(state.snapshot.contract);
        state.currentIndex = firstUnansweredIndex();
    }

    function hydrateAnswers(draftAnswers) {
        const hydrated = {};
        for (const [questionId, answer] of Object.entries(draftAnswers || {})) {
            if (!answer || typeof answer !== "object") {
                continue;
            }
            hydrated[questionId] = {
                value: answer.value === undefined ? null : answer.value,
                skipped: Boolean(answer.skipped),
                attachmentIds: Array.isArray(answer.attachmentIds) ? answer.attachmentIds.slice() : [],
                freeText: typeof answer.freeText === "string" ? answer.freeText : null
            };
        }
        return hydrated;
    }

    function flattenQuestions(contract) {
        const flattened = [];
        for (const group of contract.questionGroups || []) {
            for (const question of group.questions || []) {
                flattened.push({ ...question, groupTitle: group.title || "Questions", groupId: group.id || "" });
            }
        }
        return flattened;
    }

    function firstUnansweredIndex() {
        const index = state.questions.findIndex(question => !state.answers[question.id]);
        return index < 0 ? 0 : index;
    }

    function renderUnavailable() {
        app.replaceChildren();
        const main = node("main", "unavailable");
        main.append(node("p", "eyebrow", "LOOM / ASK USER"));
        main.append(node("h1", "", "Ask unavailable"));
        main.append(node("p", "", "This local ask link could not be opened."));
        app.append(main);
    }

    function render() {
        clearError();
        app.replaceChildren();
        const snapshot = state.snapshot;
        const shell = node("div", "shell");
        const header = node("header", "topbar");
        const brand = node("a", "brand", "LOOM / ASK USER");
        brand.href = "#top";
        brand.addEventListener("click", event => event.preventDefault());
        header.append(brand);
        const headerActions = node("div", "topbar-actions");
        const connection = node("span", "connection-status", state.offline ? "Offline copy" : "Connected to this ask");
        connection.id = "connection-status";
        connection.dataset.kind = state.offline ? "offline" : "ready";
        headerActions.append(connection);
        if (state.offline) {
            headerActions.append(button("Download answers", "button button-quiet", () => {
                void downloadAnswers().catch(showError);
            }));
        }
        header.append(headerActions);
        shell.append(header);

        const notice = node("div", "notice");
        notice.id = "notice";
        notice.setAttribute("aria-live", "polite");
        shell.append(notice);

        const workspace = node("main", "workspace");
        workspace.append(renderSidebar());
        const content = node("section", "content");
        content.setAttribute("aria-live", "polite");
        if (snapshot.receipt) {
            content.append(renderSubmitted());
        } else if (state.questions.length === 0) {
            content.append(node("h1", "", "No questions in this ask"));
        } else if (state.mode === "json") {
            content.append(renderJsonEditor());
        } else {
            content.append(renderQuestion());
        }
        workspace.append(content);
        shell.append(workspace);
        const footer = node("footer", "footer-status");
        footer.append(node("span", "", `Ask ${snapshot.askId}`));
        const saveStatus = node("span", "", state.offline ? "Changes stay in this file" : "Draft is synced locally");
        saveStatus.id = "save-status";
        footer.append(saveStatus);
        shell.append(footer);
        app.append(shell);
    }

    function renderSidebar() {
        const sidebar = node("aside", "sidebar");
        sidebar.append(node("p", "eyebrow", state.offline ? "OFFLINE COPY" : "STRUCTURED ASK"));
        sidebar.append(node("h2", "sidebar-title", "Your response"));
        const completed = state.questions.filter(question => isAnswered(question)).length;
        const progress = node("div", "progress-block");
        const progressLabel = node("div", "progress-label");
        progressLabel.append(node("span", "", "Progress"));
        progressLabel.append(node("span", "", `${completed} / ${state.questions.length}`));
        progress.append(progressLabel);
        const progressBar = node("div", "progress-track");
        const progressValue = node("span", "progress-value");
        progressValue.style.width = `${state.questions.length ? Math.round(completed / state.questions.length * 100) : 0}%`;
        progressBar.append(progressValue);
        progress.append(progressBar);
        sidebar.append(progress);

        const list = node("ol", "question-list");
        state.questions.forEach((question, index) => {
            const item = node("li", "question-list-item");
            const link = button("", "question-link", () => {
                if (state.recording) {
                    return;
                }
                state.currentIndex = index;
                render();
            });
            const questionNumber = String(index + 1).padStart(2, "0");
            const questionTitle = question.prompt || question.id;
            const complete = isAnswered(question);
            link.setAttribute("aria-current", index === state.currentIndex ? "step" : "false");
            link.setAttribute("aria-label", `${questionNumber}. ${questionTitle}${complete ? ", answered" : ""}`);
            if (complete) {
                link.dataset.complete = "true";
            }
            link.append(
                node("span", "question-number", questionNumber),
                node("span", "question-link-title", questionTitle),
                node("span", "question-status", complete ? "Done" : "")
            );
            item.append(link);
            list.append(item);
        });
        sidebar.append(list);
        const mode = node("div", "mode-control");
        mode.setAttribute("role", "group");
        mode.setAttribute("aria-label", "Answer mode");
        mode.append(modeButton("Guided", "guided"));
        mode.append(modeButton("JSON", "json"));
        sidebar.append(mode);
        return sidebar;
    }

    function modeButton(label, mode) {
        const control = button(label, "mode-button", () => {
            if (state.recording) {
                return;
            }
            state.mode = mode;
            render();
        });
        control.setAttribute("aria-pressed", String(state.mode === mode));
        return control;
    }

    function renderQuestion() {
        const question = state.questions[state.currentIndex];
        const answer = answerFor(question);
        const section = node("article", "question-view");
        const meta = node("div", "question-meta");
        meta.append(node("span", "question-group", question.groupTitle));
        meta.append(node("span", "question-count", `${String(state.currentIndex + 1).padStart(2, "0")} / ${String(state.questions.length).padStart(2, "0")}`));
        section.append(meta);
        section.append(node("h1", "question-title", question.prompt || "Untitled question"));
        if (question.intent) {
            const purpose = node("aside", "purpose-panel");
            purpose.setAttribute("aria-label", "How this answer helps");
            purpose.append(node("p", "purpose-label", "PURPOSE / HOW IT HELPS"));
            purpose.append(node("p", "purpose-copy", question.intent));
            section.append(purpose);
        }
        if (question.context) {
            const context = node("aside", "context-panel");
            context.setAttribute("aria-label", "Question context and rationale");
            context.append(node("p", "context-label", "CONTEXT / WHY THIS QUESTION"));
            context.append(node("p", "context-copy", question.context));
            section.append(context);
        }
        if (question.helpText) {
            const guidance = node("aside", "guidance-panel");
            guidance.setAttribute("aria-label", "How to answer");
            guidance.append(node("p", "guidance-label", "GUIDANCE"));
            guidance.append(node("p", "help-text", question.helpText));
            section.append(guidance);
        }
        const requirement = node("p", question.required ? "requirement required" : "requirement", question.required ? "Required" : "Optional");
        section.append(requirement);

        const field = node("div", "answer-field");
        renderField(question, answer, field);
        section.append(field);
        renderAttachmentList(question, answer, section);

        if (!question.required) {
            const skipLabel = node("label", "skip-control");
            const skip = node("input", "");
            skip.type = "checkbox";
            skip.checked = answer.skipped;
            skip.addEventListener("change", () => {
                setAnswer(question, null, skip.checked);
                render();
            });
            skipLabel.append(skip, node("span", "", "Skip this question"));
            section.append(skipLabel);
        }

        const diagnostic = validateCurrent(false).diagnostics.filter(item => item.location === `question:${question.id}`);
        if (diagnostic.length > 0 && (answer.value !== null || typeof answer.freeText === "string")) {
            const errors = node("ul", "field-errors");
            for (const item of diagnostic) {
                errors.append(node("li", "", item.message));
            }
            section.append(errors);
        }

        const footer = node("div", "question-footer");
        const nav = node("div", "navigation-actions");
        nav.append(button("Previous", "button button-quiet", () => moveQuestion(-1)));
        nav.append(button(state.currentIndex === state.questions.length - 1 ? "Review answers" : "Next", "button button-primary", () => moveQuestion(1)));
        nav.children[0].disabled = state.currentIndex === 0 || Boolean(state.recording);
        nav.children[1].disabled = Boolean(state.recording);
        footer.append(nav);
        const save = node("span", "save-hint", state.offline ? "Download answers when ready" : "Drafts save automatically");
        footer.append(save);
        section.append(footer);
        return section;
    }

    function renderField(question, answer, field) {
        switch (question.type) {
            case "singleChoice":
                for (const option of question.options || []) {
                    const label = node("label", "choice-row");
                    const input = node("input", "");
                    input.type = "radio";
                    input.name = `answer-${question.id}`;
                    input.value = option.value;
                    input.checked = answer.value === option.value;
                    input.addEventListener("change", () => {
                        setAnswer(question, option.value, false, null);
                        render();
                    });
                    label.append(input, node("span", "", option.label || option.value));
                    field.append(label);
                }
                renderOtherChoice(question, answer, field, false);
                break;
            case "multipleChoice":
                for (const option of question.options || []) {
                    const label = node("label", "choice-row");
                    const input = node("input", "");
                    input.type = "checkbox";
                    input.value = option.value;
                    input.checked = Array.isArray(answer.value) && answer.value.includes(option.value);
                    input.addEventListener("change", () => {
                        const current = answerFor(question);
                        const selected = new Set(Array.isArray(current.value) ? current.value : []);
                        if (input.checked) {
                            selected.add(option.value);
                        } else {
                            selected.delete(option.value);
                        }
                        setAnswer(question, Array.from(selected), false);
                    });
                    label.append(input, node("span", "", option.label || option.value));
                    field.append(label);
                }
                renderOtherChoice(question, answer, field, true);
                break;
            case "text": {
                const input = node("textarea", "text-input");
                input.value = typeof answer.value === "string" ? answer.value : "";
                if (question.constraints && question.constraints.maxLength !== undefined && question.constraints.maxLength !== null) {
                    input.maxLength = question.constraints.maxLength;
                }
                input.rows = 5;
                input.setAttribute("aria-label", question.prompt || "Answer");
                input.addEventListener("input", () => setAnswer(question, input.value, false));
                field.append(input);
                appendBounds(field, question.constraints, "minLength", "maxLength", "characters");
                break;
            }
            case "number": {
                const input = node("input", "text-input number-input");
                input.type = "number";
                input.step = "any";
                if (question.constraints && question.constraints.minimum !== undefined && question.constraints.minimum !== null) {
                    input.min = question.constraints.minimum;
                }
                if (question.constraints && question.constraints.maximum !== undefined && question.constraints.maximum !== null) {
                    input.max = question.constraints.maximum;
                }
                input.value = typeof answer.value === "number" ? String(answer.value) : "";
                input.setAttribute("aria-label", question.prompt || "Answer");
                input.addEventListener("input", () => setAnswer(question, input.value === "" ? null : Number(input.value), false));
                field.append(input);
                appendNumericBounds(field, question.constraints);
                renderFreeTextEditor(question, answer, field, "Additional context or a text-only alternative");
                break;
            }
            case "boolean": {
                const choices = node("div", "boolean-control");
                choices.setAttribute("role", "group");
                choices.setAttribute("aria-label", question.prompt || "Answer");
                for (const [label, value] of [["Yes", true], ["No", false]]) {
                    const control = button(label, "boolean-option", () => {
                        setAnswer(question, value, false);
                        render();
                    });
                    control.setAttribute("aria-pressed", String(answer.value === value));
                    choices.append(control);
                }
                field.append(choices);
                renderFreeTextEditor(question, answer, field, "Additional context or a text-only alternative");
                break;
            }
            case "file":
                renderUploadControl(question, field, false);
                renderFreeTextEditor(question, answer, field, "Describe the file or answer in text");
                break;
            case "audio":
                renderUploadControl(question, field, true);
                renderFreeTextEditor(question, answer, field, "Describe the recording or answer in text");
                break;
            default:
                field.append(node("p", "field-errors", `Unsupported answer type: ${question.type}`));
        }
    }

    function renderOtherChoice(question, answer, field, multiple) {
        const wrapper = node("div", "free-text-option");
        const label = node("label", "choice-row");
        const input = node("input", "");
        input.type = multiple ? "checkbox" : "radio";
        if (!multiple) {
            input.name = `answer-${question.id}`;
        }
        input.checked = typeof answer.freeText === "string";
        const optionLabel = multiple ? "Other (counts as one selection)" : "Other";
        label.append(input, node("span", "", optionLabel));

        const editor = node("textarea", "text-input free-text-input");
        editor.value = typeof answer.freeText === "string" ? answer.freeText : "";
        editor.rows = 4;
        editor.disabled = !input.checked;
        editor.setAttribute("aria-label", `${question.prompt || "Answer"}: ${optionLabel}`);
        input.addEventListener("change", () => {
            const current = answerFor(question);
            const freeText = input.checked
                ? (typeof current.freeText === "string" ? current.freeText : "")
                : null;
            const value = multiple ? (Array.isArray(current.value) ? current.value : []) : null;
            setAnswer(question, value, false, freeText);
            editor.disabled = !input.checked;
            if (input.checked) {
                editor.focus();
            }
        });
        editor.addEventListener("input", () => {
            const current = answerFor(question);
            const value = multiple ? (Array.isArray(current.value) ? current.value : []) : null;
            setAnswer(question, value, false, editor.value);
        });
        wrapper.append(label, editor);
        field.append(wrapper);
    }

    function renderFreeTextEditor(question, answer, field, labelText) {
        const label = node("label", "free-text-field");
        const caption = node("span", "free-text-label", labelText);
        const editor = node("textarea", "text-input free-text-input");
        editor.value = typeof answer.freeText === "string" ? answer.freeText : "";
        editor.rows = 4;
        editor.setAttribute("aria-label", `${question.prompt || "Answer"}: ${labelText}`);
        editor.addEventListener("input", () => {
            const current = answerFor(question);
            setAnswer(question, current.value, false, editor.value);
        });
        label.append(caption, editor);
        field.append(label);
    }

    function appendBounds(target, constraints, minimumKey, maximumKey, unit) {
        if (!constraints) {
            return;
        }
        const minimum = constraints[minimumKey];
        const maximum = constraints[maximumKey];
        if (minimum === undefined && maximum === undefined) {
            return;
        }
        let message = "";
        if (minimum !== undefined && maximum !== undefined) {
            message = `${minimum} to ${maximum} ${unit}`;
        } else if (minimum !== undefined) {
            message = `At least ${minimum} ${unit}`;
        } else {
            message = `Up to ${maximum} ${unit}`;
        }
        target.append(node("p", "field-hint", message));
    }

    function appendNumericBounds(target, constraints) {
        if (!constraints) {
            return;
        }
        if (constraints.minimum !== undefined && constraints.maximum !== undefined) {
            target.append(node("p", "field-hint", `Allowed range: ${constraints.minimum} to ${constraints.maximum}`));
        } else if (constraints.minimum !== undefined) {
            target.append(node("p", "field-hint", `Minimum: ${constraints.minimum}`));
        } else if (constraints.maximum !== undefined) {
            target.append(node("p", "field-hint", `Maximum: ${constraints.maximum}`));
        }
    }

    function renderUploadControl(question, field, audio) {
        const constraints = question.constraints || {};
        const maxBytes = constraints.maxAttachmentBytes;
        const allowed = audio ? ["audio/*"] : (constraints.allowedMediaTypes || []);
        const input = node("input", "file-input");
        input.type = "file";
        input.accept = allowed.length > 0 ? allowed.join(",") : (audio ? "audio/*" : "*/*");
        input.disabled = state.offline;
        input.setAttribute("aria-label", audio ? "Choose audio recording" : "Choose file");
        input.addEventListener("change", () => {
            const file = input.files && input.files[0];
            if (file) {
                void trackUpload(uploadAttachment(question, file)).catch(showError);
            }
            input.value = "";
        });
        field.append(input);
        if (audio) {
            const record = button(state.recording ? "Stop recording" : "Record audio", "button button-quiet", () => {
                void toggleRecording(question, record).catch(showError);
            });
            record.disabled = state.offline || !global.navigator.mediaDevices || typeof global.MediaRecorder === "undefined";
            field.append(record);
        }
        const limits = [];
        if (maxBytes) {
            limits.push(`Maximum ${formatBytes(maxBytes)}`);
        }
        if (allowed.length > 0) {
            limits.push(allowed.join(", "));
        }
        if (limits.length > 0) {
            field.append(node("p", "field-hint", limits.join(" · ")));
        }
        if (state.offline) {
            field.append(node("p", "field-hint", "New attachments are unavailable in an offline copy."));
        }
    }

    async function toggleRecording(question, control) {
        if (state.recording) {
            state.recording.recorder.stop();
            control.textContent = "Finishing recording...";
            control.disabled = true;
            return;
        }
        if (state.offline) {
            throw new Error("Audio recording requires the active local ask page.");
        }
        const stream = await global.navigator.mediaDevices.getUserMedia({ audio: true });
        const formats = ["audio/webm;codecs=opus", "audio/webm", "audio/ogg;codecs=opus", "audio/ogg", "audio/mp4"];
        const mimeType = formats.find(format => global.MediaRecorder.isTypeSupported(format));
        const recorder = mimeType ? new global.MediaRecorder(stream, { mimeType }) : new global.MediaRecorder(stream);
        const chunks = [];
        state.recording = { recorder, stream };
        recorder.addEventListener("dataavailable", event => {
            if (event.data && event.data.size > 0) {
                chunks.push(event.data);
            }
        });
        recorder.addEventListener("stop", async () => {
            stream.getTracks().forEach(track => track.stop());
            state.recording = null;
            render();
            try {
                const blob = new Blob(chunks, { type: recorder.mimeType || "audio/webm" });
                const extension = blob.type.includes("ogg") ? "ogg" : blob.type.includes("mp4") ? "m4a" : "webm";
                await trackUpload(uploadAttachment(question, new File([blob], `voice-note.${extension}`, { type: blob.type })));
            } catch (error) {
                showError(error);
            }
        });
        recorder.start();
        control.textContent = "Stop recording";
        control.dataset.recording = "true";
    }

    function trackUpload(uploadPromise) {
        const trackedUpload = uploadPromise.finally(() => state.pendingUploads.delete(trackedUpload));
        state.pendingUploads.add(trackedUpload);
        return trackedUpload;
    }

    async function uploadAttachment(question, file) {
        if (state.offline) {
            throw new Error("New attachments cannot be added from an offline copy.");
        }
        const maxBytes = question.constraints && question.constraints.maxAttachmentBytes;
        if (maxBytes && file.size > maxBytes) {
            throw new Error(`This file is larger than the ${formatBytes(maxBytes)} limit.`);
        }
        clearError();
        updateSaveStatus("Uploading attachment...");
        await persistDraft();
        const mediaType = file.type || (question.type === "audio" ? "audio/webm" : "application/octet-stream");
        const response = await fetch(`/api/attachments/${encodeURIComponent(question.id)}`, {
            method: "POST",
            headers: {
                Authorization: `Bearer ${state.token}`,
                "Content-Type": mediaType,
                "X-Ask-Generation": String(state.snapshot.generation),
                "X-File-Name": encodeURIComponent(file.name)
            },
            body: file,
            cache: "no-store",
            credentials: "omit",
            referrerPolicy: "no-referrer"
        });
        const result = await readJsonResponse(response);
        const metadata = result.attachment;
        state.snapshot.generation = result.generation;
        state.snapshot.attachments = [...(state.snapshot.attachments || []).filter(item => item.attachmentId !== metadata.attachmentId), metadata];
        const existing = answerFor(question);
        state.answers[question.id] = {
            value: null,
            skipped: false,
            attachmentIds: [metadata.attachmentId],
            freeText: existing.freeText
        };
        state.revision += 1;
        state.dirty = true;
        updateSaveStatus("Attachment uploaded");
        render();
        await persistDraft();
    }

    async function readJsonResponse(response) {
        const body = await response.json().catch(() => ({}));
        if (!response.ok) {
            const details = Array.isArray(body.diagnostics)
                ? body.diagnostics.map(item => `${item.location}: ${item.message}`).join("\n")
                : body.error;
            throw new Error(details || `The request failed with HTTP ${response.status}.`);
        }
        return body;
    }

    function renderAttachmentList(question, answer, target) {
        const ids = Array.isArray(answer.attachmentIds) ? answer.attachmentIds : [];
        for (const attachmentId of ids) {
            const metadata = (state.snapshot.attachments || []).find(item => item.attachmentId === attachmentId);
            if (!metadata) {
                continue;
            }
            const row = node("div", "attachment-row");
            const details = node("div", "attachment-details");
            details.append(node("strong", "", metadata.fileName));
            details.append(node("span", "", `${formatBytes(metadata.length)} · ${metadata.mediaType}`));
            row.append(details);
            const download = button(state.offline ? "Unavailable offline" : "Download", "button button-small button-quiet", () => {
                void downloadAttachment(metadata).catch(showError);
            });
            download.disabled = state.offline;
            row.append(download);
            row.append(button("Remove", "button button-small button-quiet", () => {
                const current = answerFor(question);
                state.answers[question.id] = {
                    value: null,
                    skipped: false,
                    attachmentIds: ids.filter(item => item !== attachmentId),
                    freeText: current.freeText
                };
                markDirty();
                render();
            }));
            target.append(row);
        }
    }

    async function downloadAttachment(metadata) {
        if (state.offline) {
            throw new Error("This attachment is only available from the active local ask page.");
        }
        const response = await fetch(`/api/attachments/${encodeURIComponent(metadata.attachmentId)}`, {
            headers: { Authorization: `Bearer ${state.token}` },
            cache: "no-store",
            credentials: "omit",
            referrerPolicy: "no-referrer"
        });
        if (!response.ok) {
            throw new Error(`The attachment could not be downloaded (HTTP ${response.status}).`);
        }
        saveBlob(await response.blob(), metadata.fileName);
    }

    function renderSubmitted() {
        const section = node("article", "submitted-view");
        section.append(node("p", "eyebrow", "SUBMISSION RECEIVED"));
        section.append(node("h1", "", "Your answers are saved"));
        section.append(node("p", "", "This ask has already been submitted to its workflow."));
        section.append(node("p", "field-hint", `Submitted ${new Date(state.snapshot.receipt.submittedAtUtc).toLocaleString()}`));
        return section;
    }

    function renderJsonEditor() {
        const section = node("article", "json-view");
        section.append(node("p", "eyebrow", "DIRECT ANSWERS"));
        section.append(node("h1", "", "Edit answers as JSON"));
        section.append(node("p", "question-intent", "Use the question IDs, values, optional freeText, and attachment IDs from the guided form."));
        const editor = node("textarea", "json-editor");
        editor.spellcheck = false;
        editor.setAttribute("aria-label", "Answers JSON");
        editor.value = JSON.stringify(state.answers, null, 2);
        section.append(editor);
        const actions = node("div", "json-actions");
        actions.append(button("Apply JSON", "button button-primary", () => {
            try {
                const parsed = JSON.parse(editor.value);
                if (!parsed || typeof parsed !== "object" || Array.isArray(parsed)) {
                    throw new Error("Answers must be a JSON object keyed by question id.");
                }
                const validation = validator.validate(state.snapshot.contract, parsed, state.snapshot.attachments, false);
                if (!validation.valid) {
                    throw new Error(validation.diagnostics.map(item => `${item.location}: ${item.message}`).join("\n"));
                }
                state.answers = parsed;
                markDirty();
                render();
            } catch (error) {
                showError(error);
            }
        }));
        actions.append(button("Copy JSON", "button button-quiet", () => {
            void copyText(editor.value).then(() => updateSaveStatus("JSON copied")).catch(showError);
        }));
        actions.append(button("Download JSON", "button button-quiet", () => {
            void downloadAnswers().catch(showError);
        }));
        if (!state.offline) {
            actions.append(button("Copy cURL", "button button-quiet", () => {
                void copyText(createCurlRequest()).then(() => updateSaveStatus("cURL request copied")).catch(showError);
            }));
        }
        section.append(actions);
        const validation = validateCurrent(false);
        if (validation.diagnostics.length > 0) {
            const errors = node("ul", "field-errors");
            for (const diagnostic of validation.diagnostics.slice(0, 8)) {
                errors.append(node("li", "", `${diagnostic.location}: ${diagnostic.message}`));
            }
            section.append(errors);
        }
        const footer = node("div", "question-footer");
        footer.append(button("Return to guided form", "button button-quiet", () => {
            state.mode = "guided";
            render();
        }));
        footer.append(button("Submit answers", "button button-primary", () => void submitAnswers()));
        section.append(footer);
        return section;
    }

    function validateCurrent(requireComplete) {
        return validator.validate(state.snapshot.contract, state.answers, state.snapshot.attachments, requireComplete);
    }

    function isAnswered(question) {
        const answer = state.answers[question.id];
        if (!answer) {
            return false;
        }
        if (answer.skipped) {
            return true;
        }
        if (typeof answer.freeText === "string" && answer.freeText.trim().length > 0) {
            return true;
        }
        if (Array.isArray(answer.attachmentIds) && answer.attachmentIds.length > 0) {
            return true;
        }
        return answer.value !== undefined && answer.value !== null && answer.value !== "";
    }

    async function moveQuestion(offset) {
        clearError();
        window.clearTimeout(saveTimer);
        if (!state.offline) {
            try {
                await persistDraft();
            } catch (error) {
                showError(error);
                return;
            }
        }
        const nextIndex = state.currentIndex + offset;
        if (nextIndex >= state.questions.length) {
            const validation = validateCurrent(true);
            if (!validation.valid) {
                showValidationErrors(validation.diagnostics);
                return;
            }
            state.mode = "json";
        } else if (nextIndex >= 0) {
            state.currentIndex = nextIndex;
        }
        render();
    }

    function showValidationErrors(diagnostics) {
        const panel = document.getElementById("notice");
        if (!panel) {
            return;
        }
        panel.replaceChildren();
        panel.className = "notice notice-error";
        panel.setAttribute("role", "alert");
        const list = node("ul", "");
        for (const item of diagnostics.slice(0, 12)) {
            list.append(node("li", "", `${item.location}: ${item.message}`));
        }
        panel.append(list);
    }

    async function persistDraft() {
        if (state.offline || !state.dirty) {
            return;
        }
        const runSave = async () => {
            while (state.dirty) {
                const revision = state.revision;
                const answers = cloneJson(state.answers);
                updateSaveStatus("Saving draft...");
                const snapshot = await request("/api/draft", {
                    method: "PUT",
                    body: {
                        expectedGeneration: state.snapshot.generation,
                        answers
                    }
                });
                state.snapshot = snapshot;
                state.questions = flattenQuestions(snapshot.contract);
                state.dirty = state.revision !== revision;
                updateSaveStatus(state.dirty ? "New changes pending" : "Draft saved");
            }
        };
        state.saveQueue = state.saveQueue.then(runSave, runSave);
        return state.saveQueue;
    }

    async function submitAnswers() {
        clearError();
        window.clearTimeout(saveTimer);
        const validation = validateCurrent(true);
        if (!validation.valid) {
            showValidationErrors(validation.diagnostics);
            return;
        }
        if (state.offline) {
            await downloadAnswers();
            updateSaveStatus("Answers downloaded; this offline copy cannot submit to the workflow");
            return;
        }
        try {
            await persistDraft();
            updateSaveStatus("Submitting answers...");
            const receipt = await request("/api/submit", {
                method: "POST",
                body: {
                    expectedGeneration: state.snapshot.generation,
                    operationId: crypto.randomUUID(),
                    answers: cloneJson(state.answers)
                }
            });
            state.snapshot.receipt = receipt;
            state.snapshot.generation = receipt.generation;
            render();
        } catch (error) {
            showError(error);
        }
    }

    async function refreshStatus() {
        try {
            const status = await request("/api/status");
            if (status.receipt && !state.snapshot.receipt) {
                state.snapshot.receipt = status.receipt;
                state.snapshot.generation = status.generation;
                render();
            }
        } catch (error) {
            if (error.message && error.message.includes("expired")) {
                updateConnectionStatus("Ask expired", "offline");
                showError(error);
            }
        }
    }


    async function downloadAnswers() {
        let validation = validateCurrent(true);
        if (!validation.valid) {
            showValidationErrors(validation.diagnostics);
            return;
        }
        if (!state.offline) {
            window.clearTimeout(saveTimer);
            await persistDraft();
            validation = validateCurrent(true);
            if (!validation.valid) {
                showValidationErrors(validation.diagnostics);
                return;
            }
        }
        const submission = {
            schemaVersion: 1,
            askId: state.snapshot.askId,
            expectedGeneration: state.snapshot.generation,
            operationId: crypto.randomUUID(),
            answers: cloneJson(state.answers)
        };
        const content = JSON.stringify(submission, null, 2);
        saveBlob(new Blob([content], { type: "application/json;charset=utf-8" }), `ask-${state.snapshot.askId}-submission.json`);
    }

    function createCurlRequest() {
        const payload = {
            expectedGeneration: state.snapshot.generation,
            operationId: crypto.randomUUID(),
            answers: cloneJson(state.answers)
        };
        const data = JSON.stringify(payload).replace(/'/g, "'\\''");
        return `curl --request POST '${global.location.origin}/api/submit' --header 'Origin: ${global.location.origin}' --header 'Authorization: Bearer ${state.token}' --header 'Content-Type: application/json' --data '${data}'`;
    }

    async function copyText(value) {
        if (global.navigator.clipboard && global.navigator.clipboard.writeText) {
            await global.navigator.clipboard.writeText(value);
            return;
        }
        const temporary = node("textarea", "copy-buffer");
        temporary.value = value;
        document.body.append(temporary);
        temporary.select();
        const copied = document.execCommand("copy");
        temporary.remove();
        if (!copied) {
            throw new Error("Clipboard access is unavailable in this browser.");
        }
    }

    function saveBlob(blob, fileName) {
        const url = URL.createObjectURL(blob);
        const link = node("a", "download-link");
        link.href = url;
        link.download = fileName;
        document.body.append(link);
        link.click();
        link.remove();
        window.setTimeout(() => URL.revokeObjectURL(url), 1000);
    }

    function formatBytes(value) {
        if (value < 1024) {
            return `${value} B`;
        }
        if (value < 1024 * 1024) {
            return `${(value / 1024).toFixed(1)} KB`;
        }
        return `${(value / (1024 * 1024)).toFixed(1)} MB`;
    }

    initialize();
}(globalThis));
