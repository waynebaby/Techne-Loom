using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class AskScopedHttpWorkerTests
{
    [Theory]
    [InlineData("ao.exe")]
    [InlineData("ao")]
    [InlineData("so.exe")]
    [InlineData("so")]
    public void WorkerLauncher_UsesAppHostDirectly(string appHostName)
    {
        var executablePath = Path.Combine("app", appHostName);
        var startInfo = AskScopedWorkerProcessLauncher.CreateStartInfo(executablePath, entryAssemblyPath: null);

        Assert.Equal(Path.GetFullPath(executablePath), startInfo.FileName);
        Assert.Equal(["--ask-user-worker"], startInfo.ArgumentList);
    }

    [Theory]
    [InlineData("dotnet", "ao")]
    [InlineData("dotnet.exe", "ao")]
    [InlineData("dotnet", "so")]
    [InlineData("dotnet.exe", "so")]
    public void WorkerLauncher_UsesDotnetExecForDotnetHost(string dotnetHostName, string productName)
    {
        var entryAssemblyPath = Path.Combine(Path.GetTempPath(), $"{productName}-{Guid.NewGuid():N}.dll");
        File.WriteAllText(entryAssemblyPath, string.Empty);
        try
        {
            var executablePath = Path.Combine("sdk", dotnetHostName);
            var startInfo = AskScopedWorkerProcessLauncher.CreateStartInfo(executablePath, entryAssemblyPath);

            Assert.Equal(Path.GetFullPath(executablePath), startInfo.FileName);
            Assert.Equal("exec", startInfo.ArgumentList[0]);
            Assert.Equal(Path.GetFullPath(entryAssemblyPath), startInfo.ArgumentList[1]);
            Assert.Equal("--ask-user-worker", startInfo.ArgumentList[2]);
            Assert.Equal(3, startInfo.ArgumentList.Count);
        }
        finally
        {
            File.Delete(entryAssemblyPath);
        }
    }

    [Theory]
    [InlineData("ao")]
    [InlineData("so")]
    public void WorkerLauncher_ResolvesEntryAssemblyFromBaseDirectory(string productName)
    {
        var baseDirectory = Path.Combine(Path.GetTempPath(), "techne-loom-app");

        Assert.Equal(
            Path.GetFullPath($"{productName}.dll", baseDirectory),
            AskScopedWorkerProcessLauncher.ResolveEntryAssemblyPath(productName, baseDirectory));
    }
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void WorkerLauncher_RejectsMissingEntryAssemblyForDotnetHost(string? entryAssemblyPath)
    {
        var executablePath = Path.Combine("sdk", OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            AskScopedWorkerProcessLauncher.CreateStartInfo(executablePath, entryAssemblyPath));

        Assert.Contains("entry assembly path is unavailable", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WorkerLauncher_RejectsMissingEntryAssemblyFileForDotnetHost()
    {
        var executablePath = Path.Combine("sdk", OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
        var missingAssemblyPath = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.dll");

        Assert.False(File.Exists(missingAssemblyPath));
        var exception = Assert.Throws<InvalidOperationException>(() =>
            AskScopedWorkerProcessLauncher.CreateStartInfo(executablePath, missingAssemblyPath));

        Assert.Contains("entry assembly path is unavailable", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Worker_ExchangesSingleUsePairingAndValidatesDraftAndSubmissionRequests()
    {
        var root = CreateRoot();
        try
        {
            var store = new AskScopedSubmissionStore(new AskScopedSubmissionStoreOptions { RootDirectory = root });
            var launch = await store.CreateAsync("workflow-http-text", CreateAsk(UserInputQuestionTypes.Text));
            await using var worker = await AskScopedHttpWorker.StartAsync(store, launch);
            using var client = new HttpClient();
            var endpoint = new Uri(worker.Endpoint.Url);
            var origin = endpoint.GetLeftPart(UriPartial.Authority);
            var pairingCode = Uri.UnescapeDataString(endpoint.Fragment["#pair=".Length..]);

            using var indexResponse = await client.GetAsync(endpoint);
            Assert.Equal(HttpStatusCode.OK, indexResponse.StatusCode);
            Assert.Contains("/answer-validator.js", await indexResponse.Content.ReadAsStringAsync());
            using var validatorResponse = await client.GetAsync(new Uri(endpoint, "answer-validator.js"));
            Assert.Equal(HttpStatusCode.OK, validatorResponse.StatusCode);
            Assert.Contains("AskUserAnswerValidator", await validatorResponse.Content.ReadAsStringAsync());

            using var unauthorized = await client.GetAsync(new Uri(endpoint, "api/ask"));
            Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

            using var exchangeRequest = CreateJsonRequest(HttpMethod.Post, new Uri(endpoint, "api/session"), new { pairingCode });
            exchangeRequest.Headers.Add("Origin", origin);
            using var exchangeResponse = await client.SendAsync(exchangeRequest);
            Assert.Equal(HttpStatusCode.OK, exchangeResponse.StatusCode);
            var exchange = await exchangeResponse.Content.ReadFromJsonAsync<SessionResponse>();
            Assert.False(string.IsNullOrWhiteSpace(exchange?.Token));
            var token = exchange!.Token;

            using var repeatedPairingRequest = CreateJsonRequest(HttpMethod.Post, new Uri(endpoint, "api/session"), new { pairingCode });
            repeatedPairingRequest.Headers.Add("Origin", origin);
            using var repeatedPairingResponse = await client.SendAsync(repeatedPairingRequest);
            Assert.Equal(HttpStatusCode.Unauthorized, repeatedPairingResponse.StatusCode);

            using var rejectedDraftRequest = CreateJsonRequest(
                HttpMethod.Put,
                new Uri(endpoint, "api/draft"),
                new { expectedGeneration = launch.Generation, answers = new Dictionary<string, AskScopedAnswerValue>() },
                token);
            rejectedDraftRequest.Headers.Add("Origin", "http://evil.example");
            using var rejectedDraftResponse = await client.SendAsync(rejectedDraftRequest);
            Assert.Equal(HttpStatusCode.Forbidden, rejectedDraftResponse.StatusCode);

            using var invalidSubmitRequest = CreateJsonRequest(
                HttpMethod.Post,
                new Uri(endpoint, "api/submit"),
                new { expectedGeneration = launch.Generation, operationId = "http-submit-invalid", answers = new Dictionary<string, AskScopedAnswerValue>() },
                token);
            invalidSubmitRequest.Headers.Add("Origin", origin);
            using var invalidSubmitResponse = await client.SendAsync(invalidSubmitRequest);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, invalidSubmitResponse.StatusCode);

            using var validSubmitRequest = CreateJsonRequest(
                HttpMethod.Post,
                new Uri(endpoint, "api/submit"),
                new
                {
                    expectedGeneration = launch.Generation,
                    operationId = "http-submit-valid",
                    answers = new Dictionary<string, AskScopedAnswerValue>(StringComparer.Ordinal)
                    {
                        ["question.answer"] = Answer(JsonSerializer.SerializeToElement("Ada")),
                    },
                },
                token);
            validSubmitRequest.Headers.Add("Origin", origin);
            using var validSubmitResponse = await client.SendAsync(validSubmitRequest);
            Assert.Equal(HttpStatusCode.OK, validSubmitResponse.StatusCode);
            var receipt = await validSubmitResponse.Content.ReadFromJsonAsync<AskScopedSubmissionReceipt>();
            Assert.Equal("Ada", receipt!.Answers.Single().Value!.Value.GetString());

            using var statusRequest = new HttpRequestMessage(HttpMethod.Get, new Uri(endpoint, "api/status"));
            statusRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var statusResponse = await client.SendAsync(statusRequest);
            Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
            using var status = JsonDocument.Parse(await statusResponse.Content.ReadAsStringAsync());
            Assert.Equal("submitted", status.RootElement.GetProperty("status").GetString());

            using var queryCredentialRequest = new HttpRequestMessage(HttpMethod.Get, new Uri(endpoint, $"api/ask?token={Uri.EscapeDataString(token)}"));
            queryCredentialRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var queryCredentialResponse = await client.SendAsync(queryCredentialRequest);
            Assert.Equal(HttpStatusCode.BadRequest, queryCredentialResponse.StatusCode);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task Worker_StreamsBoundedAttachmentAndBindsItToSubmissionQuestion()
    {
        var root = CreateRoot();
        try
        {
            var store = new AskScopedSubmissionStore(new AskScopedSubmissionStoreOptions { RootDirectory = root });
            var askTransition = CreateAsk(UserInputQuestionTypes.File);
            askTransition.UserInput!.QuestionGroups[0].Questions[0].Prompt = "{{APP_SCRIPT}}";
            var launch = await store.CreateAsync("workflow-http-file", askTransition);
            await using var worker = await AskScopedHttpWorker.StartAsync(store, launch);
            using var client = new HttpClient();
            var endpoint = new Uri(worker.Endpoint.Url);
            var origin = endpoint.GetLeftPart(UriPartial.Authority);
            var pairingCode = Uri.UnescapeDataString(endpoint.Fragment["#pair=".Length..]);
            using var exchangeRequest = CreateJsonRequest(HttpMethod.Post, new Uri(endpoint, "api/session"), new { pairingCode });
            exchangeRequest.Headers.Add("Origin", origin);
            using var exchangeResponse = await client.SendAsync(exchangeRequest);
            exchangeResponse.EnsureSuccessStatusCode();
            var token = (await exchangeResponse.Content.ReadFromJsonAsync<SessionResponse>())!.Token;

            using var uploadRequest = new HttpRequestMessage(HttpMethod.Post, new Uri(endpoint, "api/attachments/question.answer"))
            {
                Content = new ByteArrayContent([0x10, 0x20, 0x30]),
            };
            uploadRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            uploadRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            uploadRequest.Headers.Add("Origin", origin);
            uploadRequest.Headers.Add("X-File-Name", Uri.EscapeDataString("report.pdf"));
            uploadRequest.Headers.Add("X-Ask-Generation", launch.Generation.ToString(System.Globalization.CultureInfo.InvariantCulture));
            using var uploadResponse = await client.SendAsync(uploadRequest);
            Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);
            var upload = await uploadResponse.Content.ReadFromJsonAsync<AskScopedAttachmentUploadResult>();
            Assert.NotNull(upload);

            using var offlineRequest = new HttpRequestMessage(HttpMethod.Get, new Uri(endpoint, "api/offline"));
            offlineRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var offlineResponse = await client.SendAsync(offlineRequest);
            Assert.Equal(HttpStatusCode.OK, offlineResponse.StatusCode);
            var offlineHtml = await offlineResponse.Content.ReadAsStringAsync();
            Assert.Contains("AskUserAnswerValidator", offlineHtml);
            Assert.Contains("Download answers", offlineHtml, StringComparison.Ordinal);
            Assert.DoesNotContain("Save offline copy", offlineHtml, StringComparison.Ordinal);
            Assert.Contains("ask-user-bootstrap", offlineHtml);
            Assert.Contains("report.pdf", offlineHtml);
            Assert.Contains("{{APP_SCRIPT}}", offlineHtml, StringComparison.Ordinal);
            Assert.DoesNotContain("{{BOOTSTRAP}}", offlineHtml, StringComparison.Ordinal);
            Assert.DoesNotContain("{{STYLE}}", offlineHtml, StringComparison.Ordinal);
            Assert.DoesNotContain("{{VALIDATOR_SCRIPT}}", offlineHtml, StringComparison.Ordinal);

            using var submitRequest = CreateJsonRequest(
                HttpMethod.Post,
                new Uri(endpoint, "api/submit"),
                new
                {
                    expectedGeneration = upload!.Generation,
                    operationId = "http-submit-file",
                    answers = new Dictionary<string, AskScopedAnswerValue>(StringComparer.Ordinal)
                    {
                        ["question.answer"] = Answer(attachmentIds: [upload.Attachment.AttachmentId]),
                    },
                },
                token);
            submitRequest.Headers.Add("Origin", origin);
            using var submitResponse = await client.SendAsync(submitRequest);
            Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);
            var receipt = await submitResponse.Content.ReadFromJsonAsync<AskScopedSubmissionReceipt>();
            Assert.Equal(upload.Attachment.AttachmentId, receipt!.Answers.Single().Attachments.Single().AttachmentId);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task SubmittedReceipt_MapsNestedAnswersAndPreservesOptionalSkip()
    {
        var root = CreateRoot();
        try
        {
            var transition = CreateAsk(UserInputQuestionTypes.Text);
            transition.Command.Parameters!["requiredInputs"] = new[] { "answers.value", "answers.note" };
            transition.UserInput!.QuestionGroups[0].Questions.Add(new UserInputQuestion
            {
                Id = "question.note",
                Context = "Add an optional note.",
                Intent = "Capture an additional detail.",
                Prompt = "Optional note",
                ContextPath = "answers.note",
                Type = UserInputQuestionTypes.Text,
                Required = false,
            });
            var (instance, waitGroup) = CreateWaitingAskWorkflow("workflow-receipt-mapping", transition, correlationKey: "receipt-correlation");
            var waitId = waitGroup.GetNextPendingEntry()!.WaitId;
            var store = new AskScopedSubmissionStore(new AskScopedSubmissionStoreOptions { RootDirectory = root });
            var launch = await store.GetOrCreateForWaitAsync(instance, waitGroup);
            var receipt = await store.SubmitAsync(
                launch.AskId,
                launch.MachineCapability,
                launch.Generation,
                "offline-submit-mapping",
                new Dictionary<string, AskScopedAnswerValue>(StringComparer.Ordinal)
                {
                    ["question.answer"] = Answer(JsonSerializer.SerializeToElement("Ada")),
                    ["question.note"] = new AskScopedAnswerValue { Skipped = true },
                });

            var resume = await AskScopedSubmissionWorkflow.TryGetSubmittedReceiptAsync(instance, waitGroup, store);
            var recoveredResume = await AskScopedSubmissionWorkflow.GetSubmittedReceiptAsync(store, launch.AskId);

            Assert.NotNull(resume);
            Assert.Equal(waitId, resume.WaitId);
            Assert.Equal(receipt.Generation, resume.ReceiptGeneration);
            Assert.Equal(AskScopedSubmissionWorkflow.CreateResumeOperationId(receipt), resume.OperationId);
            Assert.Equal("receipt-correlation", resume.CorrelationKey);
            Assert.Equal(resume.WaitId, recoveredResume.WaitId);
            Assert.Equal(resume.TransitionId, recoveredResume.TransitionId);
            Assert.Equal(resume.CorrelationKey, recoveredResume.CorrelationKey);
            Assert.Equal(resume.OperationId, recoveredResume.OperationId);
            Assert.Equal("Ada", Assert.IsType<JsonElement>(PathValueAccessor.GetValue(resume.Payload, "answers.value")).GetString());
            Assert.Null(PathValueAccessor.GetValue(resume.Payload, "answers.note"));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task SubmittedReceipt_ProjectsFreeTextAccordingToQuestionSemantics()
    {
        var root = CreateRoot();
        try
        {
            var transition = CreateAsk(UserInputQuestionTypes.SingleChoice);
            var questions = transition.UserInput!.QuestionGroups[0].Questions;
            questions[0].Options = [new UserInputOption { Value = "known", Label = "Known" }];
            questions.AddRange(new UserInputQuestion[]
            {
                new()
                {
                    Id = "question.multiple",
                    Context = "Choose any fitting options.",
                    Intent = "Capture multiple selections.",
                    Prompt = "Which apply?",
                    ContextPath = "answers.multiple",
                    Type = UserInputQuestionTypes.MultipleChoice,
                    Required = true,
                    Options = [new UserInputOption { Value = "fixed", Label = "Fixed" }, new UserInputOption { Value = "backup", Label = "Backup" }],
                    Constraints = new UserInputQuestionConstraints { MinSelections = 2, MaxSelections = 2 },
                },
                new()
                {
                    Id = "question.number",
                    Context = "Provide a measurement.",
                    Intent = "Supply a numeric value.",
                    Prompt = "Measurement",
                    ContextPath = "answers.number",
                    Type = UserInputQuestionTypes.Number,
                    Required = true,
                    Constraints = new UserInputQuestionConstraints { Minimum = 1, Maximum = 10 },
                },
                new()
                {
                    Id = "question.boolean",
                    Context = "Indicate whether the condition is known.",
                    Intent = "Record the user's determination.",
                    Prompt = "Known?",
                    ContextPath = "answers.boolean",
                    Type = UserInputQuestionTypes.Boolean,
                    Required = true,
                },
                new()
                {
                    Id = "question.file",
                    Context = "Attach a document if available.",
                    Intent = "Include source material.",
                    Prompt = "Document",
                    ContextPath = "answers.file",
                    Type = UserInputQuestionTypes.File,
                    Required = true,
                    Constraints = new UserInputQuestionConstraints { AllowedMediaTypes = ["application/pdf"], MaxAttachmentBytes = 16 },
                },
                new()
                {
                    Id = "question.audio",
                    Context = "Record a spoken answer if available.",
                    Intent = "Capture a voice response.",
                    Prompt = "Voice response",
                    ContextPath = "answers.audio",
                    Type = UserInputQuestionTypes.Audio,
                    Required = true,
                },
            });
            transition.Command.Parameters!["requiredInputs"] = new[]
            {
                "answers.value", "answers.multiple", "answers.number", "answers.boolean", "answers.file", "answers.audio",
            };
            var (instance, waitGroup) = CreateWaitingAskWorkflow("workflow-semantic-free-text", transition);
            var store = new AskScopedSubmissionStore(new AskScopedSubmissionStoreOptions { RootDirectory = root });
            var launch = await store.GetOrCreateForWaitAsync(instance, waitGroup);
            using var content = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 });
            var upload = await store.StoreAttachmentAsync(
                launch.AskId,
                launch.MachineCapability,
                launch.Generation,
                "question.file",
                "source.pdf",
                "application/pdf",
                content);
            var receipt = await store.SubmitAsync(
                launch.AskId,
                launch.MachineCapability,
                upload.Generation,
                "semantic-free-text",
                new Dictionary<string, AskScopedAnswerValue>(StringComparer.Ordinal)
                {
                    ["question.answer"] = new AskScopedAnswerValue { FreeText = "not listed" },
                    ["question.multiple"] = new AskScopedAnswerValue { Value = JsonSerializer.SerializeToElement(new[] { "fixed" }), FreeText = "custom option" },
                    ["question.number"] = new AskScopedAnswerValue { Value = JsonSerializer.SerializeToElement(5), FreeText = "measured manually" },
                    ["question.boolean"] = new AskScopedAnswerValue { FreeText = "not sure" },
                    ["question.file"] = new AskScopedAnswerValue { FreeText = "scanned copy", AttachmentIds = [upload.Attachment.AttachmentId] },
                    ["question.audio"] = new AskScopedAnswerValue { FreeText = "microphone unavailable" },
                });

            Assert.Equal("custom option", Assert.Single(receipt.Answers, static answer => answer.QuestionId == "question.multiple").FreeText);
            var resume = await AskScopedSubmissionWorkflow.GetSubmittedReceiptAsync(store, launch.AskId);
            Assert.Equal("not listed", Assert.IsType<string>(PathValueAccessor.GetValue(resume.Payload, "answers.value")));

            var multiple = Assert.IsType<JsonElement>(PathValueAccessor.GetValue(resume.Payload, "answers.multiple"));
            Assert.Equal("fixed", multiple[0].GetString());
            Assert.Equal("custom option", multiple[1].GetString());

            var number = Assert.IsType<Dictionary<string, object?>>(PathValueAccessor.GetValue(resume.Payload, "answers.number"));
            Assert.Equal(5, Assert.IsType<JsonElement>(number["value"]).GetInt32());
            Assert.Equal("measured manually", Assert.IsType<string>(number["freeText"]));
            Assert.Equal("not sure", Assert.IsType<string>(PathValueAccessor.GetValue(resume.Payload, "answers.boolean")));

            var file = Assert.IsType<Dictionary<string, object?>>(PathValueAccessor.GetValue(resume.Payload, "answers.file"));
            Assert.Equal(upload.Attachment.AttachmentId, Assert.IsType<AskScopedAttachmentMetadata>(file["attachment"]).AttachmentId);
            Assert.Equal("scanned copy", Assert.IsType<string>(file["freeText"]));
            Assert.Equal("microphone unavailable", Assert.IsType<string>(PathValueAccessor.GetValue(resume.Payload, "answers.audio")));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task StartWorkersForActiveWaits_OnlyStartsUnsubmittedStructuredAsks()
    {
        var root = CreateRoot();
        try
        {
            var transition = CreateAsk(UserInputQuestionTypes.Text);
            var (instance, waitGroup) = CreateWaitingAskWorkflow("workflow-worker-eligibility", transition);
            var store = new AskScopedSubmissionStore(new AskScopedSubmissionStoreOptions { RootDirectory = root });
            var startCount = 0;
            Func<AskScopedSubmissionStore, AskScopedLaunch, CancellationToken, Task<AskScopedWorkerEndpoint>> startWorker =
                (_, launch, _) =>
                {
                    startCount++;
                    return Task.FromResult(new AskScopedWorkerEndpoint(launch.AskId, "http://127.0.0.1:1/#pair=code", launch.ExpiresAtUtc));
                };

            var endpoints = await AskScopedSubmissionWorkflow.StartWorkersForActiveWaitsAsync(instance, store, startWorker);
            Assert.Single(endpoints);
            Assert.Equal(1, startCount);

            var launchForSubmit = await store.GetForWaitAsync(instance, waitGroup)
                ?? throw new InvalidOperationException("The ask was not created for the active wait.");
            await store.SubmitAsync(
                launchForSubmit.AskId,
                launchForSubmit.MachineCapability,
                launchForSubmit.Generation,
                "worker-eligibility-submit",
                new Dictionary<string, AskScopedAnswerValue>(StringComparer.Ordinal)
                {
                    ["question.answer"] = Answer(JsonSerializer.SerializeToElement("Ada")),
                });

            var duplicateWaitIds = new HashSet<string>(StringComparer.Ordinal) { waitGroup.GetNextPendingEntry()!.WaitId };
            var repeatedInvocation = await AskScopedSubmissionWorkflow.StartWorkersForActiveWaitsAsync(instance, store, startWorker, existingWaitIds: duplicateWaitIds);
            Assert.Empty(repeatedInvocation);
            Assert.Equal(1, startCount);

            var afterSubmission = await AskScopedSubmissionWorkflow.StartWorkersForActiveWaitsAsync(instance, store, startWorker);
            Assert.Empty(afterSubmission);
            Assert.Equal(1, startCount);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task Worker_StopsAfterSubmissionReceiptIsPersisted()
    {
        var root = CreateRoot();
        try
        {
            var store = new AskScopedSubmissionStore(new AskScopedSubmissionStoreOptions { RootDirectory = root });
            var launch = await store.CreateAsync("workflow-http-worker-stop", CreateAsk(UserInputQuestionTypes.Text));
            await using var worker = await AskScopedHttpWorker.StartAsync(store, launch);

            await store.SubmitAsync(
                launch.AskId,
                launch.MachineCapability,
                launch.Generation,
                "worker-stop-after-receipt",
                new Dictionary<string, AskScopedAnswerValue>(StringComparer.Ordinal)
                {
                    ["question.answer"] = Answer(JsonSerializer.SerializeToElement("Ada")),
                });

            await worker.Completion;
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    private static HttpRequestMessage CreateJsonRequest(HttpMethod method, Uri uri, object body, string? token = null)
    {
        var request = new HttpRequestMessage(method, uri)
        {
            Content = new StringContent(JsonSerializer.Serialize(body, WorkflowJsonSerializer.CreateDefaultOptions(indented: false)), Encoding.UTF8, "application/json"),
        };
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return request;
    }

    private static (WorkflowInstance Instance, PendingWaitGroup WaitGroup) CreateWaitingAskWorkflow(
        string instanceId,
        CommandTransition transition,
        string? correlationKey = null)
    {
        var waitGroup = new PendingWaitGroup
        {
            InstanceId = instanceId,
            TransitionId = transition.Id,
            CorrelationKey = correlationKey,
        };
        waitGroup.AddEntry(expireAt: null);
        var instance = new WorkflowInstance
        {
            InstanceId = instanceId,
            Status = WorkflowStatus.WaitingExternal,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [transition.Id] = transition,
            },
            ActiveWaitGroups = [waitGroup],
        };
        return (instance, waitGroup);
    }

    private static CommandTransition CreateAsk(string questionType)
        => new()
        {
            Id = "transition.http-ask",
            StepKind = WorkflowStepKind.AskUser,
            Command = new CommandInvocation
            {
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["requiredInputs"] = new[] { "answers.value" },
                },
            },
            UserInput = new UserInputContract
            {
                QuestionGroups =
                [
                    new UserInputQuestionGroup
                    {
                        Id = "group.http",
                        Title = "Details",
                        Questions =
                        [
                            new UserInputQuestion
                            {
                                Id = "question.answer",
                                Context = "Answer the test question.",
                                Intent = "Exercise the HTTP worker.",
                                Prompt = "Provide a value.",
                                ContextPath = "answers.value",
                                Type = questionType,
                                Required = true,
                                Constraints = new UserInputQuestionConstraints
                                {
                                    MinLength = questionType == UserInputQuestionTypes.Text ? 1 : null,
                                    AllowedMediaTypes = questionType == UserInputQuestionTypes.File ? ["application/pdf"] : [],
                                    MaxAttachmentBytes = questionType == UserInputQuestionTypes.File ? 16 : null,
                                },
                            },
                        ],
                    },
                ],
            },
        };

    private static AskScopedAnswerValue Answer(JsonElement? value = null, List<string>? attachmentIds = null)
        => new() { Value = value, AttachmentIds = attachmentIds ?? [] };

    private static string CreateRoot()
        => Path.Combine(Path.GetTempPath(), $"techne-loom-ask-http-{Guid.NewGuid():N}");

    private static void DeleteRoot(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed record SessionResponse(string Token);
}
