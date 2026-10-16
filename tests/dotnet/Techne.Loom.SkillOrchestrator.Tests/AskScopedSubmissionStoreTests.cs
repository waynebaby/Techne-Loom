using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class AskScopedSubmissionStoreTests
{
    [Fact]
    public async Task CreateAndSaveDraft_AreIsolatedAndSurviveStoreRestart()
    {
        var root = CreateRoot();
        try
        {
            var options = new AskScopedSubmissionStoreOptions { RootDirectory = root };
            var firstStore = new AskScopedSubmissionStore(options);
            var firstLaunch = await firstStore.CreateAsync("workflow-one", CreateAsk("transition.ask-one", "answers.name"));
            var secondLaunch = await firstStore.CreateAsync("workflow-two", CreateAsk("transition.ask-two", "answers.name"));
            var saved = await firstStore.SaveDraftAsync(
                firstLaunch.AskId,
                firstLaunch.MachineCapability,
                firstLaunch.Generation,
                new Dictionary<string, AskScopedAnswerValue>(StringComparer.Ordinal)
                {
                    ["question.name"] = Answer(JsonSerializer.SerializeToElement("Ada")),
                });
            var restartedStore = new AskScopedSubmissionStore(options);
            var restored = await restartedStore.GetSnapshotAsync(firstLaunch.AskId, firstLaunch.MachineCapability);
            var untouched = await restartedStore.GetSnapshotAsync(secondLaunch.AskId, secondLaunch.MachineCapability);

            Assert.NotEqual(firstLaunch.AskId, secondLaunch.AskId);
            Assert.Equal(1, saved.Generation);
            Assert.Equal(1, restored.Generation);
            Assert.Equal("Ada", restored.DraftAnswers["question.name"].Value!.Value.GetString());
            Assert.Empty(untouched.DraftAnswers);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => restartedStore.GetSnapshotAsync(firstLaunch.AskId, secondLaunch.MachineCapability));
            if (!OperatingSystem.IsWindows())
            {
                Assert.Equal(
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
                    File.GetUnixFileMode(Path.Combine(root, firstLaunch.AskId)));
            }
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task CreateStandaloneAsk_DoesNotRequireWorkflowAndReturnsAnswersAndReceipt()
    {
        var root = CreateRoot();
        try
        {
            var store = new AskScopedSubmissionStore(new AskScopedSubmissionStoreOptions { RootDirectory = root });
            var contract = new UserInputContract
            {
                Version = 1,
                QuestionGroups =
                [
                    new UserInputQuestionGroup
                    {
                        Id = "group.standalone",
                        Title = "Standalone questions",
                        Questions =
                        [
                            new UserInputQuestion
                            {
                                Id = "question.intent",
                                Context = "The calling agent needs clarification without a workflow.",
                                Intent = "Verify the shared ask session can run without workflow identity.",
                                Prompt = "What should the standalone ask capability do?",
                                Type = UserInputQuestionTypes.Text,
                                Required = true,
                                Constraints = new UserInputQuestionConstraints { MinLength = 1, MaxLength = 120 },
                            },
                        ],
                    },
                ],
            };

            var launch = await store.CreateStandaloneAsync(contract);
            var statePath = Path.Combine(root, launch.AskId, "state.json");
            using (var state = JsonDocument.Parse(await File.ReadAllTextAsync(statePath)))
            {
                Assert.False(state.RootElement.TryGetProperty("workflowInstanceId", out _));
                Assert.False(state.RootElement.TryGetProperty("transitionId", out _));
                var question = state.RootElement.GetProperty("contract").GetProperty("questionGroups")[0].GetProperty("questions")[0];
                Assert.False(question.TryGetProperty("contextPath", out _));
            }

            var receipt = await store.SubmitAsync(
                launch.AskId,
                launch.MachineCapability,
                launch.Generation,
                "standalone-op-001",
                new Dictionary<string, AskScopedAnswerValue>(StringComparer.Ordinal)
                {
                    ["question.intent"] = Answer(JsonSerializer.SerializeToElement("Return typed answers and a receipt.")),
                });
            var result = await store.GetStandaloneResultAsync(launch.AskId);

            Assert.NotNull(result);
            Assert.Equal(launch.AskId, result.AskId);
            Assert.Equal("Return typed answers and a receipt.", result.Answers.Single().Value!.Value.GetString());
            Assert.Equal(AskScopedConsumerKind.Standalone, result.ConsumerKind);
            var submittedReceipt = Assert.IsType<AskScopedSubmissionReceipt>(result.Receipt);
            Assert.Equal(receipt.AskId, submittedReceipt.AskId);
            Assert.Equal(AskScopedConsumerKind.Standalone, submittedReceipt.ConsumerKind);
            Assert.Null(submittedReceipt.WorkflowInstanceId);
            Assert.Null(submittedReceipt.TransitionId);
            var receiptJson = JsonSerializer.Serialize(submittedReceipt, WorkflowJsonSerializer.CreateDefaultOptions(indented: false));
            Assert.DoesNotContain("workflowInstanceId", receiptJson, StringComparison.Ordinal);
            Assert.DoesNotContain("transitionId", receiptJson, StringComparison.Ordinal);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task CreateStandaloneAsk_SubmittedReceiptDoesNotConsumeActiveAskLimit()
    {
        var root = CreateRoot();
        try
        {
            var store = new AskScopedSubmissionStore(new AskScopedSubmissionStoreOptions
            {
                RootDirectory = root,
                MaxActiveAsks = 1,
            });
            var first = await store.CreateStandaloneAsync(CreateAsk("transition.standalone-one", "answers.name").UserInput!);
            await store.SubmitAsync(
                first.AskId,
                first.MachineCapability,
                first.Generation,
                "standalone-active-one",
                NameAnswer("Ada"));
            var firstResult = await store.GetStandaloneResultAsync(first.AskId);
            Assert.True(firstResult.Submitted);

            var second = await store.CreateStandaloneAsync(CreateAsk("transition.standalone-two", "answers.name").UserInput!);
            Assert.NotEqual(first.AskId, second.AskId);
            await Assert.ThrowsAsync<AskScopedConflictException>(() =>
                store.CreateStandaloneAsync(CreateAsk("transition.standalone-three", "answers.name").UserInput!));
        }
        finally
        {
            DeleteRoot(root);
        }
    }
    [Fact]
    public async Task CreateWorkflowAsk_SubmittedReceiptStillConsumesActiveAskLimit()
    {
        var root = CreateRoot();
        try
        {
            var store = new AskScopedSubmissionStore(new AskScopedSubmissionStoreOptions
            {
                RootDirectory = root,
                MaxActiveAsks = 1,
            });
            var transition = CreateAsk("transition.workflow-active-limit", "answers.name");
            var (instance, waitGroup) = CreateWaitingAskWorkflow("workflow-active-limit", transition);
            var launch = await store.GetOrCreateForWaitAsync(instance, waitGroup);
            await store.SubmitAsync(
                launch.AskId,
                launch.MachineCapability,
                launch.Generation,
                "workflow-active-receipt",
                NameAnswer("Ada"));

            var error = await Assert.ThrowsAsync<AskScopedConflictException>(() =>
                store.CreateStandaloneAsync(CreateAsk("transition.standalone-after-workflow", "answers.name").UserInput!));
            var snapshot = await store.GetSnapshotAsync(launch.AskId, launch.MachineCapability);

            Assert.Contains("active ask limit", error.Message, StringComparison.Ordinal);
            Assert.NotNull(snapshot.Receipt);
            Assert.Equal(AskScopedConsumerKind.WorkflowNode, snapshot.ConsumerKind);
        }
        finally
        {
            DeleteRoot(root);
        }
    }
    [Fact]
    public async Task GetOrCreateForWait_RecoversAskAndCapabilityAfterRestart()
    {
        var root = CreateRoot();
        try
        {
            var options = new AskScopedSubmissionStoreOptions { RootDirectory = root };
            var store = new AskScopedSubmissionStore(options);
            var transition = CreateAsk("transition.wait-recovery", "answers.name");
            var (instance, waitGroup) = CreateWaitingAskWorkflow("workflow-wait-recovery", transition);
            var waitId = waitGroup.GetNextPendingEntry()!.WaitId;
            var missingLaunch = await store.GetForWaitAsync(instance, waitGroup);
            Assert.Null(missingLaunch);
            Assert.Empty(Directory.EnumerateDirectories(root));
            var launch = await store.GetOrCreateForWaitAsync(instance, waitGroup);
            var capabilityPath = Path.Combine(root, launch.AskId, "machine-capability");
            var statePath = Path.Combine(root, launch.AskId, "state.json");
            var stateJson = await File.ReadAllTextAsync(statePath);

            Assert.DoesNotContain(launch.MachineCapability, stateJson, StringComparison.Ordinal);
            Assert.Equal(launch.MachineCapability, await File.ReadAllTextAsync(capabilityPath));
            if (!OperatingSystem.IsWindows())
            {
                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(capabilityPath));
            }

            var restartedStore = new AskScopedSubmissionStore(options);
            var restoredLaunch = await restartedStore.GetForWaitAsync(instance, waitGroup)
                ?? throw new InvalidOperationException("The persisted wait ask was not found after restart.");
            var restoredById = await restartedStore.GetLaunchAsync(launch.AskId);
            Assert.NotNull(restoredById);
            Assert.Equal(launch.MachineCapability, restoredById.MachineCapability);
            var restoredSnapshot = await restartedStore.GetSnapshotAsync(restoredLaunch.AskId, restoredLaunch.MachineCapability);
            Assert.Equal(launch.AskId, restoredLaunch.AskId);
            Assert.Equal(launch.MachineCapability, restoredLaunch.MachineCapability);
            Assert.Equal(waitId, restoredSnapshot.WaitId);
            Assert.Equal("ask-correlation", restoredSnapshot.CorrelationKey);

            var wrongCorrelationWaitGroup = new PendingWaitGroup
            {
                InstanceId = instance.InstanceId,
                TransitionId = transition.Id,
                CorrelationKey = "different-correlation",
                Entries = [new PendingWaitEntry { WaitId = waitId }],
            };
            var wrongCorrelationInstance = new WorkflowInstance
            {
                InstanceId = instance.InstanceId,
                Status = WorkflowStatus.WaitingExternal,
                Nodes = instance.Nodes,
                ActiveWaitGroups = [wrongCorrelationWaitGroup],
            };
            var mismatch = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                restartedStore.GetForWaitAsync(wrongCorrelationInstance, wrongCorrelationWaitGroup));
            Assert.Contains("correlation key", mismatch.Message, StringComparison.Ordinal);

            var (nextInstance, nextWaitGroup) = CreateWaitingAskWorkflow(instance.InstanceId, transition);
            var nextLaunch = await restartedStore.GetOrCreateForWaitAsync(nextInstance, nextWaitGroup);
            Assert.NotEqual(launch.AskId, nextLaunch.AskId);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task SaveDraft_RejectsStaleGenerationAndUnknownQuestions()
    {
        var root = CreateRoot();
        try
        {
            var store = new AskScopedSubmissionStore(new AskScopedSubmissionStoreOptions { RootDirectory = root });
            var launch = await store.CreateAsync("workflow-draft", CreateAsk("transition.draft", "answers.name"));
            await store.SaveDraftAsync(
                launch.AskId,
                launch.MachineCapability,
                launch.Generation,
                new Dictionary<string, AskScopedAnswerValue>(StringComparer.Ordinal)
                {
                    ["question.name"] = Answer(JsonSerializer.SerializeToElement("Ada")),
                });

            await Assert.ThrowsAsync<AskScopedConflictException>(() => store.SaveDraftAsync(
                launch.AskId,
                launch.MachineCapability,
                launch.Generation,
                new Dictionary<string, AskScopedAnswerValue>(StringComparer.Ordinal)));
            await Assert.ThrowsAsync<AskScopedValidationException>(() => store.SaveDraftAsync(
                launch.AskId,
                launch.MachineCapability,
                expectedGeneration: 1,
                new Dictionary<string, AskScopedAnswerValue>(StringComparer.Ordinal)
                {
                    ["question.unknown"] = Answer(JsonSerializer.SerializeToElement("value")),
                }));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task Submit_ReplaysSameOperationAndRejectsDifferentPayloadOrOperation()
    {
        var root = CreateRoot();
        try
        {
            var options = new AskScopedSubmissionStoreOptions { RootDirectory = root };
            var store = new AskScopedSubmissionStore(options);
            var launch = await store.CreateAsync("workflow-submit", CreateAsk("transition.submit", "answers.name"));
            var answers = NameAnswer("Ada");
            var first = await store.SubmitAsync(launch.AskId, launch.MachineCapability, launch.Generation, "ask-submit-1", answers);
            var restartedStore = new AskScopedSubmissionStore(options);
            var replay = await restartedStore.SubmitAsync(launch.AskId, launch.MachineCapability, expectedGeneration: 0, "ask-submit-1", NameAnswer("Ada"));
            var snapshot = await restartedStore.GetSnapshotAsync(launch.AskId, launch.MachineCapability);

            Assert.Equal(first.Generation, replay.Generation);
            Assert.Equal(first.RequestHash, replay.RequestHash);
            Assert.Equal(first.IntegrityHash, replay.IntegrityHash);
            Assert.Equal("Ada", replay.Answers.Single().Value!.Value.GetString());
            Assert.Equal(first.Generation, snapshot.Generation);
            Assert.Equal(first.Generation, snapshot.Receipt!.Generation);
            await Assert.ThrowsAsync<AskScopedConflictException>(() => restartedStore.SubmitAsync(
                launch.AskId,
                launch.MachineCapability,
                expectedGeneration: 0,
                "ask-submit-1",
                NameAnswer("Grace")));
            await Assert.ThrowsAsync<AskScopedConflictException>(() => restartedStore.SubmitAsync(
                launch.AskId,
                launch.MachineCapability,
                expectedGeneration: 0,
                "ask-submit-2",
                NameAnswer("Ada")));

            var applied = await restartedStore.MarkAppliedAsync(launch.AskId, launch.MachineCapability, first.Generation);
            Assert.NotNull(applied.AppliedAtUtc);
            var appliedAt = applied.AppliedAtUtc;
            var replayedApplied = await restartedStore.MarkAppliedAsync(launch.AskId, launch.MachineCapability, first.Generation);
            Assert.Equal(appliedAt, replayedApplied.AppliedAtUtc);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task GetSnapshot_RejectsTamperedReceiptIntegrityHash()
    {
        var root = CreateRoot();
        try
        {
            var options = new AskScopedSubmissionStoreOptions { RootDirectory = root };
            var store = new AskScopedSubmissionStore(options);
            var launch = await store.CreateAsync("workflow-tampered", CreateAsk("transition.tampered", "answers.name"));
            await store.SubmitAsync(launch.AskId, launch.MachineCapability, launch.Generation, "ask-tampered-1", NameAnswer("Ada"));
            var statePath = Path.Combine(root, launch.AskId, "state.json");
            var state = System.Text.Json.Nodes.JsonNode.Parse(await File.ReadAllTextAsync(statePath))!;
            var integrityHash = state["receipt"]!["integrityHash"]!.GetValue<string>();
            state["receipt"]!["integrityHash"] = (integrityHash[0] == '0' ? '1' : '0') + integrityHash[1..];
            await File.WriteAllTextAsync(statePath, state.ToJsonString());

            await Assert.ThrowsAsync<InvalidOperationException>(() => store.GetSnapshotAsync(launch.AskId, launch.MachineCapability));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task Submit_CompetingControllersHaveSingleGenerationWinner()
    {
        var root = CreateRoot();
        try
        {
            var options = new AskScopedSubmissionStoreOptions { RootDirectory = root };
            var launch = await new AskScopedSubmissionStore(options).CreateAsync("workflow-race", CreateAsk("transition.race", "answers.name"));
            var firstStore = new AskScopedSubmissionStore(options);
            var secondStore = new AskScopedSubmissionStore(options);
            var first = CaptureSubmissionAsync(firstStore, launch, "ask-race-1", "Ada");
            var second = CaptureSubmissionAsync(secondStore, launch, "ask-race-2", "Grace");
            var results = await Task.WhenAll(first, second);

            Assert.Equal(1, results.Count(static succeeded => succeeded));
            var snapshot = await firstStore.GetSnapshotAsync(launch.AskId, launch.MachineCapability);
            Assert.NotNull(snapshot.Receipt);
            Assert.Contains(snapshot.Receipt.Answers.Single().Value!.Value.GetString(), new[] { "Ada", "Grace" });
        }
        finally
        {
            DeleteRoot(root);
        }
    }


    [Fact]
    public async Task StoreAttachment_PersistsBytesBindsQuestionAndVerifiesIntegrity()
    {
        var root = CreateRoot();
        try
        {
            var store = new AskScopedSubmissionStore(new AskScopedSubmissionStoreOptions { RootDirectory = root });
            var launch = await store.CreateAsync("workflow-attachment", CreateAttachmentAsk("transition.attachment", "answers.upload", includeSecondQuestion: true));
            var content = new byte[] { 0x10, 0x20, 0x30, 0x40 };
            var uploaded = await store.StoreAttachmentAsync(
                launch.AskId,
                launch.MachineCapability,
                launch.Generation,
                "question.name",
                "report.pdf",
                "application/pdf",
                new MemoryStream(content, writable: false));
            var snapshot = await store.GetSnapshotAsync(launch.AskId, launch.MachineCapability);

            Assert.Equal(1, uploaded.Generation);
            Assert.Equal(uploaded.Attachment, Assert.Single(snapshot.Attachments));
            Assert.Equal("question.name", uploaded.Attachment.QuestionId);
            await Assert.ThrowsAsync<AskScopedValidationException>(() => store.SaveDraftAsync(
                launch.AskId,
                launch.MachineCapability,
                uploaded.Generation,
                new Dictionary<string, AskScopedAnswerValue>(StringComparer.Ordinal)
                {
                    ["question.other"] = new AskScopedAnswerValue { AttachmentIds = [uploaded.Attachment.AttachmentId] },
                }));

            await using (var attachmentStream = await store.OpenAttachmentReadStreamAsync(
                launch.AskId,
                launch.MachineCapability,
                uploaded.Attachment.AttachmentId))
            {
                using var restoredContent = new MemoryStream();
                await attachmentStream.CopyToAsync(restoredContent);
                Assert.Equal(content, restoredContent.ToArray());
            }

            var receipt = await store.SubmitAsync(
                launch.AskId,
                launch.MachineCapability,
                uploaded.Generation,
                "ask-attachment-1",
                new Dictionary<string, AskScopedAnswerValue>(StringComparer.Ordinal)
                {
                    ["question.name"] = new AskScopedAnswerValue { AttachmentIds = [uploaded.Attachment.AttachmentId] },
                    ["question.other"] = new AskScopedAnswerValue { Skipped = true },
                });
            Assert.Equal(uploaded.Attachment, receipt.Answers.Single(static answer => answer.QuestionId == "question.name").Attachments.Single());

            var attachmentPath = Path.Combine(root, launch.AskId, "attachments", $"{uploaded.Attachment.AttachmentId}.bin");
            await File.WriteAllBytesAsync(attachmentPath, [0x10, 0x20, 0x30, 0x41]);
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.OpenAttachmentReadStreamAsync(
                launch.AskId,
                launch.MachineCapability,
                uploaded.Attachment.AttachmentId));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task StoreAttachment_DefaultLimitsAllowFiftyMiBFile()
    {
        var root = CreateRoot();
        try
        {
            var store = new AskScopedSubmissionStore(new AskScopedSubmissionStoreOptions { RootDirectory = root });
            var launch = await store.CreateAsync("workflow-attachment-50-mib", CreateAttachmentAsk(
                "transition.attachment-50-mib",
                "answers.upload",
                maxAttachmentBytes: 50L * 1024 * 1024));
            using var content = new MemoryStream(new byte[50 * 1024 * 1024], writable: false);

            var uploaded = await store.StoreAttachmentAsync(
                launch.AskId,
                launch.MachineCapability,
                launch.Generation,
                "question.name",
                "maximum.pdf",
                "application/pdf",
                content);
            var snapshot = await store.GetSnapshotAsync(launch.AskId, launch.MachineCapability);

            Assert.Equal(50L * 1024 * 1024, uploaded.Attachment.Length);
            Assert.Equal(uploaded.Attachment, Assert.Single(snapshot.Attachments));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task StoreAttachment_EnforcesQuestionAndPerAskLimits()
    {
        var root = CreateRoot();
        try
        {
            var store = new AskScopedSubmissionStore(new AskScopedSubmissionStoreOptions
            {
                RootDirectory = root,
                MaxAttachmentBytes = 16,
                MaxAttachmentsPerAsk = 1,
            });
            var launch = await store.CreateAsync("workflow-attachment-limits", CreateAttachmentAsk(
                "transition.attachment-limits",
                "answers.upload",
                maxAttachmentBytes: 4));

            await Assert.ThrowsAsync<AskScopedValidationException>(() => store.StoreAttachmentAsync(
                launch.AskId,
                launch.MachineCapability,
                launch.Generation,
                "question.name",
                "image.png",
                "image/png",
                new MemoryStream([0x01])));
            await Assert.ThrowsAsync<AskScopedValidationException>(() => store.StoreAttachmentAsync(
                launch.AskId,
                launch.MachineCapability,
                launch.Generation,
                "question.name",
                "large.pdf",
                "application/pdf",
                new MemoryStream([0x01, 0x02, 0x03, 0x04, 0x05])));

            var uploaded = await store.StoreAttachmentAsync(
                launch.AskId,
                launch.MachineCapability,
                launch.Generation,
                "question.name",
                "small.pdf",
                "application/pdf",
                new MemoryStream([0x01, 0x02, 0x03, 0x04]));
            await Assert.ThrowsAsync<AskScopedConflictException>(() => store.StoreAttachmentAsync(
                launch.AskId,
                launch.MachineCapability,
                uploaded.Generation,
                "question.name",
                "second.pdf",
                "application/pdf",
                new MemoryStream([0x01])));
            var snapshot = await store.GetSnapshotAsync(launch.AskId, launch.MachineCapability);
            Assert.Equal(1, snapshot.Generation);
            Assert.Single(snapshot.Attachments);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task StoreAttachment_EnforcesGlobalByteLimitAndAudioMediaType()
    {
        var root = CreateRoot();
        try
        {
            var store = new AskScopedSubmissionStore(new AskScopedSubmissionStoreOptions
            {
                RootDirectory = root,
                MaxAttachmentBytes = 3,
            });
            var fileAsk = await store.CreateAsync("workflow-global-limit", CreateAttachmentAsk(
                "transition.global-limit",
                "answers.file",
                maxAttachmentBytes: 8));
            await Assert.ThrowsAsync<AskScopedValidationException>(() => store.StoreAttachmentAsync(
                fileAsk.AskId,
                fileAsk.MachineCapability,
                fileAsk.Generation,
                "question.name",
                "large.pdf",
                "application/pdf",
                new MemoryStream([0x01, 0x02, 0x03, 0x04])));

            var audioAsk = await store.CreateAsync("workflow-audio", CreateAttachmentAsk(
                "transition.audio",
                "answers.audio",
                allowedMediaType: "audio/*",
                maxAttachmentBytes: 8,
                questionType: UserInputQuestionTypes.Audio));
            await Assert.ThrowsAsync<AskScopedValidationException>(() => store.StoreAttachmentAsync(
                audioAsk.AskId,
                audioAsk.MachineCapability,
                audioAsk.Generation,
                "question.name",
                "recording.wav",
                "application/pdf",
                new MemoryStream([0x01])));
            var uploaded = await store.StoreAttachmentAsync(
                audioAsk.AskId,
                audioAsk.MachineCapability,
                audioAsk.Generation,
                "question.name",
                "recording.wav",
                "audio/wav",
                new MemoryStream([0x01, 0x02]));

            Assert.Equal(1, uploaded.Generation);
            Assert.Equal("audio/wav", uploaded.Attachment.MediaType);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task StoreAttachment_EnforcesAskAndStoreByteQuotas()
    {
        var probeRoot = CreateRoot();
        long askDirectoryBytes;
        long storeDirectoryBytes;
        try
        {
            var probeStore = new AskScopedSubmissionStore(new AskScopedSubmissionStoreOptions { RootDirectory = probeRoot });
            var probeLaunch = await probeStore.CreateAsync(
                "workflow-quota",
                CreateAttachmentAsk("transition.quota", "answers.upload", maxAttachmentBytes: 4096));
            askDirectoryBytes = GetDirectoryBytesForTest(Path.Combine(probeRoot, probeLaunch.AskId));
            storeDirectoryBytes = GetDirectoryBytesForTest(probeRoot);
        }
        finally
        {
            DeleteRoot(probeRoot);
        }

        await AssertAttachmentQuotaRejectedAsync(askDirectoryBytes + 128, long.MaxValue);
        await AssertAttachmentQuotaRejectedAsync(long.MaxValue, storeDirectoryBytes + 128);
    }

    [Fact]
    public async Task Create_EnforcesActiveAskLimit()
    {
        var root = CreateRoot();
        try
        {
            var store = new AskScopedSubmissionStore(new AskScopedSubmissionStoreOptions
            {
                RootDirectory = root,
                MaxActiveAsks = 1,
            });
            await store.CreateAsync("workflow-active-one", CreateAsk("transition.active-one", "answers.name"));

            await Assert.ThrowsAsync<AskScopedConflictException>(() => store.CreateAsync(
                "workflow-active-two",
                CreateAsk("transition.active-two", "answers.name")));
        }
        finally
        {
            DeleteRoot(root);
        }
    }
    [Fact]
    public async Task Cleanup_UsesDraftAndAppliedReceiptRetentionWindows()
    {
        var root = CreateRoot();
        try
        {
            var clock = new TestClock(new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero));
            var options = new AskScopedSubmissionStoreOptions
            {
                RootDirectory = root,
                DraftLifetime = TimeSpan.FromHours(1),
                UnappliedReceiptLifetime = TimeSpan.FromHours(2),
                AppliedReceiptLifetime = TimeSpan.FromHours(1),
            };
            var store = new AskScopedSubmissionStore(options, clock);
            var draft = await store.CreateAsync("workflow-draft-expiry", CreateAsk("transition.draft-expiry", "answers.name"));
            var submitted = await store.CreateAsync("workflow-submitted-expiry", CreateAttachmentAsk("transition.submitted-expiry", "answers.upload", maxAttachmentBytes: 16));
            var uploaded = await store.StoreAttachmentAsync(
                submitted.AskId,
                submitted.MachineCapability,
                submitted.Generation,
                "question.name",
                "retained.pdf",
                "application/pdf",
                new MemoryStream([0x01, 0x02, 0x03]));
            var receipt = await store.SubmitAsync(
                submitted.AskId,
                submitted.MachineCapability,
                uploaded.Generation,
                "ask-expiry-1",
                new Dictionary<string, AskScopedAnswerValue>(StringComparer.Ordinal)
                {
                    ["question.name"] = new AskScopedAnswerValue { AttachmentIds = [uploaded.Attachment.AttachmentId] },
                });
            var attachmentPath = Path.Combine(root, submitted.AskId, "attachments", $"{uploaded.Attachment.AttachmentId}.bin");
            var abandonedAskDirectory = Path.Combine(root, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(abandonedAskDirectory);

            clock.Advance(TimeSpan.FromMinutes(90));
            var afterDraftExpiry = await store.CleanupExpiredAsync();
            Assert.Equal(2, afterDraftExpiry.RemovedAsks);
            Assert.False(Directory.Exists(abandonedAskDirectory));
            await Assert.ThrowsAsync<FileNotFoundException>(() => store.GetSnapshotAsync(draft.AskId, draft.MachineCapability));
            Assert.NotNull(await store.GetSnapshotAsync(submitted.AskId, submitted.MachineCapability));
            Assert.True(File.Exists(attachmentPath));

            await store.MarkAppliedAsync(submitted.AskId, submitted.MachineCapability, receipt.Generation);
            clock.Advance(TimeSpan.FromMinutes(61));
            var afterAppliedExpiry = await store.CleanupExpiredAsync();
            Assert.Equal(1, afterAppliedExpiry.RemovedAsks);
            await Assert.ThrowsAsync<FileNotFoundException>(() => store.GetSnapshotAsync(submitted.AskId, submitted.MachineCapability));
            Assert.False(File.Exists(attachmentPath));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    private static async Task<bool> CaptureSubmissionAsync(
        AskScopedSubmissionStore store,
        AskScopedLaunch launch,
        string operationId,
        string answer)
    {
        try
        {
            await store.SubmitAsync(launch.AskId, launch.MachineCapability, launch.Generation, operationId, NameAnswer(answer));
            return true;
        }
        catch (AskScopedConflictException)
        {
            return false;
        }
    }

    private static async Task AssertAttachmentQuotaRejectedAsync(long maxAskBytes, long maxStoreBytes)
    {
        var root = CreateRoot();
        try
        {
            var store = new AskScopedSubmissionStore(new AskScopedSubmissionStoreOptions
            {
                RootDirectory = root,
                MaxAskBytes = maxAskBytes,
                MaxStoreBytes = maxStoreBytes,
            });
            var launch = await store.CreateAsync(
                "workflow-quota",
                CreateAttachmentAsk("transition.quota", "answers.upload", maxAttachmentBytes: 4096));

            await Assert.ThrowsAsync<AskScopedConflictException>(() => store.StoreAttachmentAsync(
                launch.AskId,
                launch.MachineCapability,
                launch.Generation,
                "question.name",
                "quota.pdf",
                "application/pdf",
                new MemoryStream([0x01, 0x02])));
            var snapshot = await store.GetSnapshotAsync(launch.AskId, launch.MachineCapability);
            Assert.Equal(0, snapshot.Generation);
            Assert.Empty(snapshot.Attachments);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    private static long GetDirectoryBytesForTest(string directory)
        => Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Sum(static path => new FileInfo(path).Length);

    private static CommandTransition CreateAttachmentAsk(
        string transitionId,
        string contextPath,
        string allowedMediaType = "application/pdf",
        long? maxAttachmentBytes = 1024,
        string questionType = UserInputQuestionTypes.File,
        bool includeSecondQuestion = false)
    {
        var transition = CreateAsk(transitionId, contextPath);
        var question = transition.UserInput!.QuestionGroups[0].Questions[0];
        question.Type = questionType;
        question.Constraints = new UserInputQuestionConstraints
        {
            AllowedMediaTypes = [allowedMediaType],
            MaxAttachmentBytes = maxAttachmentBytes,
        };
        if (includeSecondQuestion)
        {
            transition.UserInput.QuestionGroups[0].Questions.Add(new UserInputQuestion
            {
                Id = "question.other",
                Context = "Attach a separate report.",
                Intent = "Provide another report.",
                Prompt = "Choose the second report.",
                ContextPath = "answers.other",
                Type = UserInputQuestionTypes.File,
                Required = false,
                Constraints = new UserInputQuestionConstraints
                {
                    AllowedMediaTypes = ["application/pdf"],
                    MaxAttachmentBytes = 1024,
                },
            });
        }

        if (includeSecondQuestion)
        {
            transition.Command.Parameters!["requiredInputs"] = new[] { contextPath, "answers.other" };
        }

        return transition;
    }

    private static (WorkflowInstance Instance, PendingWaitGroup WaitGroup) CreateWaitingAskWorkflow(
        string instanceId,
        CommandTransition transition)
    {
        var waitGroup = new PendingWaitGroup
        {
            InstanceId = instanceId,
            TransitionId = transition.Id,
            CorrelationKey = "ask-correlation",
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

    private static CommandTransition CreateAsk(string transitionId, string contextPath)
        => new()
        {
            Id = transitionId,
            StepKind = WorkflowStepKind.AskUser,
            TargetNodeId = "state.done",
            Command = new CommandInvocation
            {
                Name = "ask_user",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["requiredInputs"] = new[] { contextPath },
                },
            },
            UserInput = new UserInputContract
            {
                QuestionGroups =
                [
                    new UserInputQuestionGroup
                    {
                        Id = "group.answers",
                        Title = "Answers",
                        Questions =
                        [
                            new UserInputQuestion
                            {
                                Id = "question.name",
                                Context = "Choose a name for the report.",
                                Intent = "Personalize the report.",
                                Prompt = "What name should we use?",
                                ContextPath = contextPath,
                                Type = UserInputQuestionTypes.Text,
                                Required = true,
                                Constraints = new UserInputQuestionConstraints { MinLength = 1, MaxLength = 80 },
                            },
                        ],
                    },
                ],
            },
        };

    private static Dictionary<string, AskScopedAnswerValue> NameAnswer(string value)
        => new(StringComparer.Ordinal)
        {
            ["question.name"] = Answer(JsonSerializer.SerializeToElement(value)),
        };

    private static AskScopedAnswerValue Answer(JsonElement? value = null)
        => new() { Value = value };

    private static string CreateRoot()
        => Path.Combine(Path.GetTempPath(), $"techne-loom-asks-{Guid.NewGuid():N}");

    private static void DeleteRoot(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class TestClock(DateTimeOffset initialTime) : ISystemClock
    {
        private DateTimeOffset _now = initialTime;

        public DateTimeOffset UtcNow => _now;

        public void Advance(TimeSpan amount) => _now += amount;
    }
}
