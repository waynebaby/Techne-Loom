using System.Diagnostics;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;
using Techne.Loom.SkillOrchestrator.Analysis;
using Techne.Loom.SkillOrchestrator.Runtime;
using Techne.Loom.SkillOrchestrator.TaskTracking;
using Techne.Loom.SkillOrchestrator.Visualizer;

namespace Techne.Loom.SkillOrchestrator.Tests;

public abstract class SkillOrchestratorBehaviorTestBase
{
    protected static WorkflowInstance CreateImmediateTimeoutWorkflow()
    {
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.ask",
                    Strategy = ConcurrencyStrategy.FirstSuccess,
                    GroupTimeout = TimeSpan.Zero,
                    TimeoutTargetStateId = "state.timeout",
                    TransitionIds = ["transition.ask"],
                },
            ],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };

        var timeout = new StateNode
        {
            Id = "state.timeout",
            Name = "Timed Out",
            Groups = [],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };

        var ask = new CommandTransition
        {
            Id = "transition.ask",
            Name = "Ask user",
            Description = "Need external input",
            TargetNodeId = timeout.Id,
            StepKind = WorkflowStepKind.AskUser,
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "noop",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal),
            },
        };

        return new WorkflowInstance
        {
            InstanceId = "timeout-wf",
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = timeout.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [timeout.Id] = timeout,
                [ask.Id] = ask,
            },
            Context = new Dictionary<string, object?>(StringComparer.Ordinal),
        };
    }

    protected static WorkflowInstance CreateChainedWorkflow(bool includeUnownedTransition = false)
    {
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            WorkflowPhase = "Intake",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.start",
                    TransitionIds = ["transition.first"],
                },
            ],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };

        var mid = new StateNode
        {
            Id = "state.mid",
            Name = "Mid",
            WorkflowPhase = "Execution",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.mid",
                    TransitionIds = ["transition.second"],
                },
            ],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };

        var done = new StateNode
        {
            Id = "state.done",
            Name = "Done",
            WorkflowPhase = "Done",
            Groups = [],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };

        var first = new CommandTransition
        {
            Id = "transition.first",
            Name = "First",
            WorkflowPhase = "Intake",
            TargetNodeId = mid.Id,
            StepKind = WorkflowStepKind.StateUpdate,
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "sample.first",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal),
            },
        };

        var second = new CommandTransition
        {
            Id = "transition.second",
            Name = "Second",
            WorkflowPhase = "Execution",
            TargetNodeId = done.Id,
            StepKind = WorkflowStepKind.StateUpdate,
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "sample.second",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal),
            },
        };

        var nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
        {
            [start.Id] = start,
            [mid.Id] = mid,
            [done.Id] = done,
            [first.Id] = first,
            [second.Id] = second,
        };

        if (includeUnownedTransition)
        {
            nodes["transition.detached"] = new CommandTransition
            {
                Id = "transition.detached",
                Name = "Detached",
                WorkflowPhase = "Detached",
                TargetNodeId = done.Id,
                StepKind = WorkflowStepKind.StateUpdate,
                Command = new CommandInvocation
                {
                    Kind = CommandInvocationKind.Tool,
                    Name = "sample.detached",
                    Parameters = new Dictionary<string, object?>(StringComparer.Ordinal),
                },
            };
        }

        return new WorkflowInstance
        {
            InstanceId = "chainsample",
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = done.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = nodes,
            Context = new Dictionary<string, object?>(StringComparer.Ordinal),
        };
    }

    protected static WorkflowInstance CreateStepKindColorWorkflow()
    {
        var states = new[]
        {
            new StateNode { Id = "state.ai", Name = "AI", WorkflowPhase = "Planning", Groups = [new TransitionGroup { Id = "group.ai", TransitionIds = ["transition.ai"] }] },
            new StateNode { Id = "state.tool", Name = "Tool", WorkflowPhase = "Execution", Groups = [new TransitionGroup { Id = "group.tool", TransitionIds = ["transition.tool"] }] },
            new StateNode { Id = "state.optional", Name = "Optional", WorkflowPhase = "Decision", Groups = [new TransitionGroup { Id = "group.optional", TransitionIds = ["transition.optional"] }] },
            new StateNode { Id = "state.required", Name = "Required", WorkflowPhase = "Review", Groups = [new TransitionGroup { Id = "group.required", TransitionIds = ["transition.required"] }] },
            new StateNode { Id = "state.done", Name = "Done", WorkflowPhase = "Done", Groups = [] },
            new StateNode { Id = "state.gate", Name = "Gate", WorkflowPhase = "Review", Groups = [] },
            new StateNode { Id = "state.default", Name = "Default", WorkflowPhase = "Review", Groups = [new TransitionGroup { Id = "group.default", TransitionIds = ["transition.default"] }] },
        };
        var transitions = new TransitionBase[]
        {
            CreateCommandTransition("transition.ai", "AI work", "state.tool", WorkflowStepKind.ModelThink),
            CreateCommandTransition("transition.tool", "Tool work", "state.optional", WorkflowStepKind.ToolCall),
            CreateCommandTransition("transition.optional", "Optional branch", "state.required", WorkflowStepKind.ConditionBranch, ownedInputMode: "user"),
            CreateCommandTransition("transition.required", "Required input", "state.done", WorkflowStepKind.AskUser),
            CreateCommandTransition("transition.default", "Default work", "state.done", (WorkflowStepKind)int.MaxValue),
        };

        return new WorkflowInstance
        {
            InstanceId = "color-sample",
            StartNodeId = "state.ai",
            CurrentNodeId = "state.ai",
            EndNodeId = "state.done",
            Nodes = states.Cast<ITaskNode>().Concat(transitions).ToDictionary(static node => node.Id, static node => node, StringComparer.Ordinal),
        };
    }

    protected static WorkflowInstance CreateGenericBranchColorWorkflow()
    {
        var start = new StateNode
        {
            Id = "state.branch",
            Name = "Branch",
            WorkflowPhase = "Decision",
            Groups = [new TransitionGroup { Id = "group.branch", TransitionIds = ["transition.branch"] }],
        };
        var done = new StateNode { Id = "state.done", Name = "Done", WorkflowPhase = "Done", Groups = [] };
        var branch = CreateCommandTransition("transition.branch", "Branch", done.Id, WorkflowStepKind.ConditionBranch);

        return new WorkflowInstance
        {
            InstanceId = "generic-branch-sample",
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = done.Id,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [done.Id] = done,
                [branch.Id] = branch,
            },
        };
    }

    protected static WorkflowInstance CreateWorkflowPhaseWorkflow()
    {
        var intake = new StateNode
        {
            Id = "state.intake",
            Name = "Intake",
            WorkflowPhase = "Intake",
            Groups = [new TransitionGroup { Id = "group.intake", TransitionIds = ["transition.intake"] }],
        };
        var plan = new StateNode
        {
            Id = "state.plan",
            Name = "Plan",
            WorkflowPhase = "Planning",
            Groups = [new TransitionGroup { Id = "group.plan", TransitionIds = ["transition.plan"] }],
        };
        var review = new StateNode
        {
            Id = "state.review",
            Name = "Review",
            WorkflowPhase = "Review",
            Groups = [],
        };

        var first = CreateCommandTransition("transition.intake", "Intake step", plan.Id, WorkflowStepKind.StateUpdate) with { WorkflowPhase = "Intake" };
        var second = CreateCommandTransition("transition.plan", "Plan step", review.Id, WorkflowStepKind.ModelThink) with { WorkflowPhase = "Planning" };

        return new WorkflowInstance
        {
            InstanceId = "phase-sample",
            StartNodeId = intake.Id,
            CurrentNodeId = intake.Id,
            EndNodeId = review.Id,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [intake.Id] = intake,
                [plan.Id] = plan,
                [review.Id] = review,
                [first.Id] = first,
                [second.Id] = second,
            },
        };
    }

    protected static WorkflowInstance CreateWorkflowPhaseCollisionWorkflow()
    {
        var first = new StateNode
        {
            Id = "state.first",
            Name = "First",
            WorkflowPhase = "Plan A",
            Groups = [new TransitionGroup { Id = "group.first", TransitionIds = ["transition.first"] }],
        };
        var second = new StateNode
        {
            Id = "state.second",
            Name = "Second",
            WorkflowPhase = "Plan-A",
            Groups = [new TransitionGroup { Id = "group.second", TransitionIds = ["transition.second"] }],
        };
        var third = new StateNode
        {
            Id = "state.third",
            Name = "Third",
            WorkflowPhase = "Plan/A",
            Groups = [],
        };

        var firstTransition = CreateCommandTransition("transition.first", "First step", second.Id, WorkflowStepKind.StateUpdate) with { WorkflowPhase = "Plan A" };
        var secondTransition = CreateCommandTransition("transition.second", "Second step", third.Id, WorkflowStepKind.ModelThink) with { WorkflowPhase = "Plan-A" };

        return new WorkflowInstance
        {
            InstanceId = "phase-collision-sample",
            StartNodeId = first.Id,
            CurrentNodeId = first.Id,
            EndNodeId = third.Id,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [first.Id] = first,
                [second.Id] = second,
                [third.Id] = third,
                [firstTransition.Id] = firstTransition,
                [secondTransition.Id] = secondTransition,
            },
        };
    }

    protected sealed record DocumentCopyManifestFixture(
        string TargetSkillPath,
        string ManifestRelativePath,
        string MapRelativePath,
        string DocumentRelativePath,
        string PackageRoot);

    protected static async Task<DocumentCopyManifestFixture> CreateDocumentCopyManifestFixtureAsync(
        string? sourceSha256 = null,
        string lockVersion = "0.3.253-beta",
        string contentMode = "full-document",
        string sourceVersion = "0.3.253-beta",
        string? sourcePackageId = "Techne.Loom.SkillOrchestrator.Runtime.win-x64",
        string? sourcePackageRid = "win-x64",
        string? sourcePackagePath = "tools/win-x64/docs/en/guides/so-guide-reference-contracts.md",
        string targetDocumentContent = "# Contract reference\n# Source contract\n",
        bool includeDocumentInMap = true,
        string? mapExtraPath = null,
        bool includePackageRoot = true)
    {
        var targetSkillPath = Path.Combine(Path.GetTempPath(), $"techne-loom-document-manifest-{Guid.NewGuid():N}");
        var referenceRoot = Path.Combine(targetSkillPath, "assets", "so-workflow", "reference", "so");
        var packageRoot = Path.Combine(targetSkillPath, "extracted-package");
        var runtimeRoot = Path.Combine(packageRoot, "tools", "win-x64");
        var packageGuidePath = Path.Combine(runtimeRoot, "docs", "en", "guides", "so-guide-reference-contracts.md");
        var packageRuntimeManifestPath = Path.Combine(runtimeRoot, "runtime.json");
        var sourceRelativePath = "docs/en/guides/so-guide-reference-contracts.md";
        var sourcePath = Path.Combine(targetSkillPath, sourceRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var packageLockPath = Path.Combine(targetSkillPath, "assets", "so-workflow", "so-package-lock.json");
        var manifestRelativePath = "assets/so-workflow/reference/document-copy-manifest.json";
        var mapRelativePath = "assets/so-workflow/node-to-file-map.md";
        var documentRelativePath = "assets/so-workflow/reference/so/runtime-contracts.md";
        var manifestPath = Path.Combine(targetSkillPath, manifestRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var mapPath = Path.Combine(targetSkillPath, mapRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var documentPath = Path.Combine(targetSkillPath, documentRelativePath.Replace('/', Path.DirectorySeparatorChar));

        Directory.CreateDirectory(referenceRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        if (includePackageRoot)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(packageGuidePath)!);
        }
        await File.WriteAllTextAsync(documentPath, targetDocumentContent);
        await File.WriteAllTextAsync(sourcePath, "# Source contract\n");
        if (includePackageRoot)
        {
            await File.WriteAllTextAsync(packageGuidePath, "# Source contract\n");
            await File.WriteAllTextAsync(
                packageRuntimeManifestPath,
                JsonSerializer.Serialize(new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["schema"] = "techne-loom-runtime-v1",
                    ["product"] = "so",
                    ["package_id"] = "Techne.Loom.SkillOrchestrator.Runtime.win-x64",
                    ["version"] = "0.3.253-beta",
                    ["rid"] = "win-x64",
                    ["docs_root"] = "tools/win-x64/docs/en",
                }));
        }
        var sourceHashPath = includePackageRoot ? packageGuidePath : sourcePath;
        var actualSourceSha256 = ComputeCanonicalDocumentHash(sourceHashPath);
        await File.WriteAllTextAsync(packageLockPath, JsonSerializer.Serialize(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["package_id"] = "Techne.Loom.SkillOrchestrator",
            ["channel"] = "beta",
            ["resolved_version"] = lockVersion,
        }));

        var mapContent = "# Node To File Map" + Environment.NewLine
            + "All checked-in document paths in this map are relative to the target skill root." + Environment.NewLine
            + "| Node | File |" + Environment.NewLine
            + "| --- | --- |" + Environment.NewLine
            + "| inspect | `assets/so-workflow/reference/document-copy-manifest.json`";
        if (includeDocumentInMap)
        {
            mapContent += " and `assets/so-workflow/reference/so/runtime-contracts.md`";
        }
        if (!string.IsNullOrWhiteSpace(mapExtraPath))
        {
            mapContent += " and `" + mapExtraPath + "`";
        }
        mapContent += " |" + Environment.NewLine;
        await File.WriteAllTextAsync(mapPath, mapContent);

        var manifest = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["schema_version"] = "1",
            ["target_skill_root"] = "target",
            ["target_bound_product"] = "so",
            ["target_bound_channel"] = "beta",
            ["target_bound_version"] = "0.3.253-beta",
            ["documents"] = new object?[]
            {
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["target_path"] = documentRelativePath,
                    ["source_path"] = sourceRelativePath,
                    ["source_product"] = "so",
                    ["source_channel"] = "beta",
                    ["source_version"] = sourceVersion,
                    ["source_sha256"] = sourceSha256 ?? actualSourceSha256,
                    ["source_package_id"] = sourcePackageId,
                    ["source_package_rid"] = sourcePackageRid,
                    ["source_package_path"] = sourcePackagePath,
                    ["content_mode"] = contentMode,
                    ["artifact_origin"] = "verified-copy",
                    ["authority_scope"] = "target-local context only",
                    ["refreshed_by"] = "test",
                },
            },
        };
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest));

        return new DocumentCopyManifestFixture(targetSkillPath, manifestRelativePath, mapRelativePath, documentRelativePath, packageRoot);
    }
    protected static WorkflowInstance CreateCheckedInAssetMemoryReadWorkflow(
        string? assetRootInput = "target_skill_path",
        string? assetRootPath = null,
        IReadOnlyList<object?>? checkedInAssets = null,
        string? documentCopyManifestPath = null,
        string? nodeToFileMapPath = null,
        string? documentCopySourceRootPath = null)
    {
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            Groups = [new TransitionGroup { Id = "group.inspect", TransitionIds = ["transition.inspect"] }],
        };
        var done = new StateNode
        {
            Id = "state.done",
            Name = "Done",
            Groups = [],
        };

        var inspect = CreateCommandTransition("transition.inspect", "Inspect assets", done.Id, WorkflowStepKind.MemoryRead) with
        {
            OutputPath = "inspection",
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "Inspect assets",
                Parameters = BuildCheckedInAssetParameters(assetRootInput, assetRootPath, checkedInAssets, documentCopyManifestPath, nodeToFileMapPath, documentCopySourceRootPath),
            },
        };

        return new WorkflowInstance
        {
            InstanceId = "memory-read-assets",
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = done.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [done.Id] = done,
                [inspect.Id] = inspect,
            },
        };
    }

    protected static Dictionary<string, object?> BuildCheckedInAssetParameters(
        string? assetRootInput,
        string? assetRootPath,
        IReadOnlyList<object?>? checkedInAssets,
        string? documentCopyManifestPath,
        string? nodeToFileMapPath,
        string? documentCopySourceRootPath)
    {
        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["checkedInAssets"] = checkedInAssets?.ToList() ?? new List<object?> { "SKILL.md" },
        };

        if (assetRootInput is not null)
        {
            parameters["assetRootInput"] = assetRootInput;
        }

        if (assetRootPath is not null)
        {
            parameters["assetRootPath"] = assetRootPath;
        }

        if (documentCopyManifestPath is not null)
        {
            parameters["documentCopyManifestPath"] = documentCopyManifestPath;
        }

        if (nodeToFileMapPath is not null)
        {
            parameters["nodeToFileMapPath"] = nodeToFileMapPath;
        }

        if (documentCopySourceRootPath is not null)
        {
            parameters["documentCopySourceRootPath"] = documentCopySourceRootPath;
        }

        return parameters;
    }

    protected static WorkflowInstance CreateInvalidOutputBindingsWorkflow()
    {
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            Groups = [new TransitionGroup { Id = "group.run", TransitionIds = ["transition.run"] }],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };

        var done = new StateNode
        {
            Id = "state.done",
            Name = "Done",
            Groups = [],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };

        var run = CreateCommandTransition("transition.run", "Run tool", done.Id, WorkflowStepKind.ToolCall) with
        {
            OutputPath = "tool.result",
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "echo",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["message"] = "ok",
                    ["outputBindings"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["tool.bound"] = "$unknown",
                    },
                },
            },
        };

        return new WorkflowInstance
        {
            InstanceId = $"invalid-output-bindings-{Guid.NewGuid():N}",
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = done.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [done.Id] = done,
                [run.Id] = run,
            },
            Context = new Dictionary<string, object?>(StringComparer.Ordinal),
        };
    }

    protected static WorkflowInstance CreateSelfReferentialOutputBindingsWorkflow()
    {
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            Groups = [new TransitionGroup { Id = "group.run", TransitionIds = ["transition.run"] }],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };

        var done = new StateNode
        {
            Id = "state.done",
            Name = "Done",
            Groups = [],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };

        var run = CreateCommandTransition("transition.run", "Run tool", done.Id, WorkflowStepKind.ToolCall) with
        {
            OutputPath = "tool.result",
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "echo",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["message"] = "ok",
                    ["outputBindings"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["tool.result.copy"] = "$result",
                    },
                },
            },
        };

        return new WorkflowInstance
        {
            InstanceId = $"self-ref-output-bindings-{Guid.NewGuid():N}",
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = done.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [done.Id] = done,
                [run.Id] = run,
            },
            Context = new Dictionary<string, object?>(StringComparer.Ordinal),
        };
    }

    protected static WorkflowInstance CreateAnalysisWorkflow()
    {
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.switch",
                    TransitionIds = ["transition.ask", "transition.wait", "transition.loop"],
                },
            ],
        };
        var wait = new StateNode { Id = "state.wait", Name = "Wait", Groups = [] };
        var done = new StateNode { Id = "state.done", Name = "Done", Groups = [] };
        var ask = CreateCommandTransition("transition.ask", "Ask", done.Id, WorkflowStepKind.AskUser) with
        {
            OwnedInputMode = "user",
            GuardExpression = "requiresUserChoice",
            SatisfiesGateIds = ["gate.workflow"],
            PublishesOutputFamilies = ["workflow_json"],
        };
        ask.Command.Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["requiredInputs"] = new List<object?> { "plan_confirmation" },
        };
        var runtimeWait = CreateCommandTransition("transition.wait", "Runtime wait", wait.Id, WorkflowStepKind.WaitResume) with
        {
            OwnedInputMode = "runtime",
        };
        var loop = new ExpressionTransition
        {
            Id = "transition.loop",
            Name = "Loop",
            TargetNodeId = start.Id,
            StepKind = WorkflowStepKind.ConditionBranch,
        };

        return new WorkflowInstance
        {
            InstanceId = "analysis-sample",
            TemplateKind = "so-governed-target-skill",
            TaskType = "skill_enhancement",
            WorkflowKind = "target_skill_enhancement",
            CaseId = "test-case",
            RunId = "test-run",
            Validation = new WorkflowValidationContract
            {
                DeclaredUserOwnedFields = ["plan_confirmation"],
                ReservedRuntimeOwnedFields = ["workflow_file"],
                Gates = new Dictionary<string, WorkflowValidationGate>(StringComparer.Ordinal)
                {
                    ["gate.workflow"] = new WorkflowValidationGate { PassExpression = "context.Has(\"workflow_json\")", RequiredOutputFamilies = ["workflow_json"] },
                },
            },
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = done.Id,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [wait.Id] = wait,
                [done.Id] = done,
                [ask.Id] = ask,
                [runtimeWait.Id] = runtimeWait,
                [loop.Id] = loop,
            },
        };
    }

    protected static CommandTransition CreateCommandTransition(string id, string name, string targetNodeId, WorkflowStepKind stepKind, string? ownedInputMode = null)
    {
        return new CommandTransition
        {
            Id = id,
            Name = name,
            TargetNodeId = targetNodeId,
            StepKind = stepKind,
            OwnedInputMode = ownedInputMode,
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = name,
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal),
            },
        };
    }

    protected static WorkflowInstance CreateEscapedCommandWorkflow()
    {
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            WorkflowPhase = "Execution",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.cmd",
                    TransitionIds = ["transition.cmd"],
                },
            ],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };

        var end = new StateNode
        {
            Id = "state.done",
            Name = "Done",
            WorkflowPhase = "Done",
            Groups = [],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };

        var command = new CommandTransition
        {
            Id = "transition.cmd",
            Name = "Danger echo",
            Description = "Emit stdout and stderr",
            TargetNodeId = end.Id,
            OutputPath = "echoOutput",
            StepKind = WorkflowStepKind.ToolCall,
            WorkflowPhase = "Execution",
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.CommandLine,
                Name = GetEscapedCommandName(),
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["args"] = GetEscapedCommandArguments(),
                },
            },
        };

        return new WorkflowInstance
        {
            InstanceId = $"cli-wf-{Guid.NewGuid():N}",
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = end.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [end.Id] = end,
                [command.Id] = command,
            },
            Context = new Dictionary<string, object?>(StringComparer.Ordinal),
        };
    }

    protected static WorkflowInstance CreateNoProgressWorkflow()
    {
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            WorkflowPhase = "Evaluation",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.never",
                    TransitionIds = ["transition.never"],
                },
            ],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };

        var transition = new ExpressionTransition
        {
            Id = "transition.never",
            Name = "Never",
            GuardExpression = "false",
            SucceedExpression = "false",
            StepKind = WorkflowStepKind.ConditionBranch,
            WorkflowPhase = "Evaluation",
        };

        return new WorkflowInstance
        {
            InstanceId = $"no-progress-wf-{Guid.NewGuid():N}",
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [transition.Id] = transition,
            },
            Context = new Dictionary<string, object?>(StringComparer.Ordinal),
        };
    }

    protected static WorkflowInstance CreateResumeWorkflow()
    {
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            WorkflowPhase = "Intake",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.ask",
                    TransitionIds = ["transition.ask"],
                },
            ],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };

        var review = new StateNode
        {
            Id = "state.review",
            Name = "Review",
            WorkflowPhase = "Review",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.review",
                    TransitionIds = ["transition.check"],
                },
            ],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };

        var done = new StateNode
        {
            Id = "state.done",
            Name = "Done",
            WorkflowPhase = "Done",
            Groups = [],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };

        var ask = new CommandTransition
        {
            Id = "transition.ask",
            Name = "Ask user",
            Description = "Need structured result",
            TargetNodeId = review.Id,
            StepKind = WorkflowStepKind.AskUser,
            WorkflowPhase = "Intake",
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "noop",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal),
            },
        };

        var check = new ExpressionTransition
        {
            Id = "transition.check",
            Name = "Check review",
            TargetNodeId = done.Id,
            StepKind = WorkflowStepKind.ConditionBranch,
            SucceedExpression = "context.Get<bool>(\"review.approved\")",
            GuardExpression = "true",
            WorkflowPhase = "Review",
        };

        return new WorkflowInstance
        {
            InstanceId = $"resume-wf-{Guid.NewGuid():N}",
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = done.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [review.Id] = review,
                [done.Id] = done,
                [ask.Id] = ask,
                [check.Id] = check,
            },
            Context = new Dictionary<string, object?>(StringComparer.Ordinal),
        };
    }

    protected static WorkflowInstance CreateGovernedWorkflow()
    {
        const string evidencePredicate = "context.Has(\"mcp_startup_evidence\") && (context.Get<string>(\"mcp_startup_evidence.transport\") == \"mcp_stdio\" || (context.Get<string>(\"mcp_startup_evidence.transport\") == \"cli\" && (context.Get<string>(\"mcp_startup_evidence.fallback_reason\") == \"mcp_transport_unavailable\" || context.Get<string>(\"mcp_startup_evidence.fallback_reason\") == \"mcp_handshake_unsupported\" || context.Get<string>(\"mcp_startup_evidence.fallback_reason\") == \"mcp_tool_unavailable\"))) && context.Get<string>(\"mcp_startup_evidence.runtime_version\") != null && context.Get<string>(\"mcp_startup_evidence.launch_descriptor\") != null && context.Get<string>(\"mcp_startup_evidence.operation_id\") != null && context.Get<string>(\"mcp_startup_evidence.workflow_file\") != null && context.Get<string>(\"mcp_startup_evidence.workflow_sha256\") != null && context.Get<bool>(\"mcp_startup_evidence.fragment_bounded\") == true && context.Get<string>(\"mcp_startup_evidence.result_sha256\") != null && (context.Get<string>(\"mcp_startup_evidence.transport\") == \"cli\" || (context.Get<bool>(\"mcp_startup_evidence.initialized\") == true && context.Get<bool>(\"mcp_startup_evidence.tool_called\") == true && context.Get<string>(\"mcp_startup_evidence.tool_name\") == \"so_inspect_workflow_fragment\"))";
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            WorkflowPhase = "Runtime Proof",
            Groups = [new TransitionGroup { Id = "group.runtime", TransitionIds = ["transition.runtime_preflight"] }],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };
        var governanceEntry = new StateNode
        {
            Id = "state.governance_entry",
            Name = "Governance Entry",
            WorkflowPhase = "Runtime Proof",
            Groups = [new TransitionGroup { Id = "group.governance_entry", TransitionIds = ["transition.mcp_first"] }],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };
        var assessment = new StateNode
        {
            Id = "state.assessment",
            Name = "Assessment",
            WorkflowPhase = "Assessment",
            Groups = [new TransitionGroup { Id = "group.emit", TransitionIds = ["transition.emit_assessment"] }],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };
        var done = new StateNode
        {
            Id = "state.done",
            Name = "Done",
            WorkflowPhase = "Done",
            Groups = [],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };
        var runtimePreflight = new CommandTransition
        {
            Id = "transition.runtime_preflight",
            Name = "Runtime preflight",
            TargetNodeId = governanceEntry.Id,
            OutputPath = "resolved_so_runtime",
            StepKind = WorkflowStepKind.WaitResume,
            GuardExpression = "true",
            SucceedExpression = "context.Has(\"resolved_so_runtime\")",
            PublishesOutputFamilies = ["runtime_preflight_result", "mcp_registration_attempt_evidence", "governance_entry_transport", "runtime_launch_descriptor_ref"],
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "workflow.reacquireRuntimeBundle",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["mcpPreflightExempt"] = true,
                    ["runtimePreflight"] = true,
                    ["mcpRegistrationRequired"] = false,
                    ["runtimeLaunchDescriptorOutput"] = "runtime_launch_descriptor_ref",
                    ["runtimeLaunchSelection"] = "runtime_owned",
                    ["mcpConfigFormats"] = new object?[] { "vscode", "claude" },
                    ["mcpConfigOutputDirectory"] = "<execution-output-root>/mcp-registration",
                    ["mcpRegistrationAttemptOutput"] = "mcp_registration_attempt_evidence",
                    ["resumeOutputKey"] = "resolved_so_runtime",
                    ["projectionMode"] = "canonical",
                    ["requiredInputs"] = new object?[] { "resolved_so_runtime", "runtime_preflight_result", "mcp_registration_attempt_evidence", "governance_entry_transport", "runtime_launch_descriptor_ref" },
                    ["outputBindings"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["runtime_preflight_result"] = "$context:runtime_preflight_result",
                        ["mcp_registration_attempt_evidence"] = "$context:mcp_registration_attempt_evidence",
                        ["governance_entry_transport"] = "$context:governance_entry_transport",
                        ["runtime_launch_descriptor_ref"] = "$result",
                    },
                },
            },
        };
        var mcpFirst = new CommandTransition
        {
            Id = "transition.mcp_first",
            Name = "Use MCP governance entry",
            TargetNodeId = assessment.Id,
            OutputPath = "mcp_startup_evidence",
            StepKind = WorkflowStepKind.McpCall,
            GuardExpression = "context.Get<string>(\"governance_entry_transport\") == \"mcp_stdio\" && context.Get<string>(\"mcp_registration_attempt_evidence.status\") == \"ready\" && context.Get<bool>(\"mcp_registration_attempt_evidence.mcp_attempted\") == true",
            SucceedExpression = evidencePredicate,
            SatisfiesGateIds = ["gate.bootstrap_mcp_ready"],
            PublishesOutputFamilies = ["mcp_startup_evidence"],
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "so_inspect_workflow_fragment",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["governanceEntry"] = true,
                    ["mcpFirst"] = true,
                    ["entryTransport"] = "mcp_stdio",
                    ["transport"] = "stdio",
                    ["requiredTool"] = "so_inspect_workflow_fragment",
                    ["runtimeLaunchDescriptorInput"] = "runtime_launch_descriptor_ref",
                    ["runtimeLaunchSelection"] = "runtime_owned",
                    ["mcpConfigRequired"] = false,
                    ["mcpRequired"] = false,
                    ["mcpConfigFormats"] = new object?[] { "vscode", "claude" },
                    ["mcpConfigOutputDirectory"] = "<execution-output-root>/mcp-registration",
                    ["mcpRegistrationAttemptInput"] = "mcp_registration_attempt_evidence",
                    ["resumeOutputKey"] = "mcp_startup_evidence",
                    ["projectionMode"] = "canonical",
                    ["workflowFileInput"] = "current_external_workflow_copy",
                    ["runtimeCommand"] = "descriptor_owned_mcp_stdio",
                    ["serverNameTemplate"] = "loom-so-{resolved_runtime_version}",
                    ["operationIdInput"] = "operation_id",
                    ["outputBindings"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["mcp_startup_evidence"] = "$result",
                    },
                    ["requiredInputs"] = new object?[] { "mcp_startup_evidence", "mcp_startup_evidence.operation_id", "runtime_launch_descriptor_ref", "mcp_registration_attempt_evidence", "operation_id" },
                    ["mustMatchPayloadInputs"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["operation_id"] = "mcp_startup_evidence.operation_id",
                    },
                },
            },
        };
        var emit = new CommandTransition
        {
            Id = "transition.emit_assessment",
            Name = "Emit assessment",
            Description = "Publish machine-readable and human-reviewable assessment outputs.",
            TargetNodeId = done.Id,
            OutputPath = "assessment_summary_json",
            StepKind = WorkflowStepKind.ArtifactEmit,
            TerminalRoutes = ["evaluation_only"],
            SatisfiesGateIds = ["gate.assessment"],
            PublishesOutputFamilies = ["assessment_summary_json", "assessment_report_md"],
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "workflow.emitAssessment",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["path"] = Path.Combine(Path.GetTempPath(), "techne-loom-assessment.md"),
                    ["content"] = "assessment",
                    ["outputBindings"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["assessment_report_md"] = "$context:assessment_summary_json",
                    },
                },
            },
        };
        return new WorkflowInstance
        {
            InstanceId = $"governed-valid-{Guid.NewGuid():N}",
            TemplateKind = "so-governed-target-skill",
            TaskType = "skill_enhancement",
            WorkflowKind = "target_skill_enhancement",
            CaseId = "test-case",
            RunId = "test-run",
            RuntimeBinding = "dotnet-so",
            Validation = CreateGovernedValidationContract(),
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = done.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [governanceEntry.Id] = governanceEntry,
                [assessment.Id] = assessment,
                [done.Id] = done,
                [runtimePreflight.Id] = runtimePreflight,
                [mcpFirst.Id] = mcpFirst,

                [emit.Id] = emit,
            },
            Context = new Dictionary<string, object?>(StringComparer.Ordinal),
        };
    }

    protected static WorkflowInstance CreateWorkflowMissingPhase()
    {
        var workflow = CreateGovernedWorkflow();
        var start = Assert.IsType<StateNode>(workflow.Nodes["state.start"]);
        start.WorkflowPhase = null;
        workflow.Nodes[start.Id] = start;
        return workflow;
    }

    protected static WorkflowInstance CreateGovernanceOnlyDoneWorkflow()
    {
        var workflow = CreateGovernedWorkflow();
        var emit = Assert.IsType<CommandTransition>(workflow.Nodes["transition.emit_assessment"]);
        workflow.Nodes[emit.Id] = emit with
        {
            SatisfiesGateIds = [],
            PublishesOutputFamilies = [],
        };

        return workflow;
    }

    protected static WorkflowInstance CreateAskUserRuntimeOwnedFieldWorkflow()
    {
        var workflow = CreateResumeWorkflow();
        workflow.TemplateKind = "so-governed-target-skill";
        workflow.Validation = CreateGovernedValidationContract();
        var ask = Assert.IsType<CommandTransition>(workflow.Nodes["transition.ask"]);
        ask = ask with
        {
            OwnedInputMode = "user",
        };
        ask.Command.Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["requiredInputs"] = new List<object?> { "workflow_file" },
        };
        workflow.Nodes[ask.Id] = ask;
        return workflow;
    }

    protected static WorkflowInstance CreateBlockedRouteWorkflowMissingOutputs()
    {
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.blocked",
                    TransitionIds = ["transition.wait_for_fix"],
                },
            ],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };

        var wait = new StateNode
        {
            Id = "state.wait",
            Name = "Wait",
            Groups = [],
            WaitBehavior = WaitBehavior.WaitForSignal,
        };

        var blocked = new CommandTransition
        {
            Id = "transition.wait_for_fix",
            Name = "Wait for fix",
            Description = "Pause after publishing strongest-earned blocked artifacts.",
            TargetNodeId = wait.Id,
            StepKind = WorkflowStepKind.WaitResume,
            BlockedRoutes = ["layout_candidate"],
            SatisfiesGateIds = ["gate.blocked_candidate"],
            PublishesBlockedOutputFamilies = ["layout_artifact"],
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "workflow.waitForFix",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal),
            },
        };

        return new WorkflowInstance
        {
            InstanceId = $"governed-blocked-{Guid.NewGuid():N}",
            TemplateKind = "explicit-workflow-graph",
            Validation = new WorkflowValidationContract
            {
                Gates = new Dictionary<string, WorkflowValidationGate>(StringComparer.Ordinal)
                {
                    ["gate.blocked_candidate"] = new WorkflowValidationGate
                    {
                        PassExpression = "context.Get<bool>(\"gate_outputs_present\")",
                        RequiredOutputFamilies = ["layout_artifact", "validator_output"],
                    },
                },
                Routes = new Dictionary<string, WorkflowRouteValidationProfile>(StringComparer.Ordinal)
                {
                    ["layout_candidate"] = new WorkflowRouteValidationProfile
                    {
                        RequiredBlockedGateIds = ["gate.blocked_candidate"],
                    },
                },
            },
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [wait.Id] = wait,
                [blocked.Id] = blocked,
            },
            Context = new Dictionary<string, object?>(StringComparer.Ordinal),
        };
    }

    protected static WorkflowInstance CreateBlockedRouteWorkflowReusingCompileGate()
    {
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.start",
                    TransitionIds = ["transition.compile_candidate"],
                },
            ],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };

        var review = new StateNode
        {
            Id = "state.review",
            Name = "Review",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.review",
                    TransitionIds = ["transition.wait_for_fix"],
                },
            ],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };

        var wait = new StateNode
        {
            Id = "state.wait",
            Name = "Wait",
            Groups = [],
            WaitBehavior = WaitBehavior.WaitForSignal,
        };

        var compile = new CommandTransition
        {
            Id = "transition.compile_candidate",
            Name = "Compile candidate",
            Description = "Compile review artifacts before the blocked boundary.",
            TargetNodeId = review.Id,
            StepKind = WorkflowStepKind.ToolCall,
            SatisfiesGateIds = ["gate.blocked_candidate"],
            PublishesOutputFamilies = ["layout_artifact", "validator_output"],
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "workflow.compileCandidate",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal),
            },
        };

        var blocked = new CommandTransition
        {
            Id = "transition.wait_for_fix",
            Name = "Wait for fix",
            Description = "Pause after publishing strongest-earned blocked artifacts.",
            TargetNodeId = wait.Id,
            StepKind = WorkflowStepKind.WaitResume,
            BlockedRoutes = ["layout_candidate"],
            SatisfiesGateIds = ["gate.blocked_candidate"],
            PublishesBlockedOutputFamilies = ["layout_artifact", "validator_output"],
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "workflow.waitForFix",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["path"] = Path.Combine(Path.GetTempPath(), "techne-loom-assessment.md"),
                    ["content"] = "assessment",
                    ["outputBindings"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["assessment_report_md"] = "$context:assessment_summary_json",
                    },
                },
            },
        };

        return new WorkflowInstance
        {
            InstanceId = $"governed-blocked-reuse-{Guid.NewGuid():N}",
            TemplateKind = "explicit-workflow-graph",
            Validation = new WorkflowValidationContract
            {
                Gates = new Dictionary<string, WorkflowValidationGate>(StringComparer.Ordinal)
                {
                    ["gate.blocked_candidate"] = new WorkflowValidationGate
                    {
                        PassExpression = "context.Get<bool>(\"gate_outputs_present\")",
                        RequiredOutputFamilies = ["layout_artifact", "validator_output"],
                    },
                },
                Routes = new Dictionary<string, WorkflowRouteValidationProfile>(StringComparer.Ordinal)
                {
                    ["layout_candidate"] = new WorkflowRouteValidationProfile
                    {
                        RequiredBlockedGateIds = ["gate.blocked_candidate"],
                    },
                },
            },
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [review.Id] = review,
                [wait.Id] = wait,
                [compile.Id] = compile,
                [blocked.Id] = blocked,
            },
            Context = new Dictionary<string, object?>(StringComparer.Ordinal),
        };
    }

    protected static WorkflowValidationContract CreateGovernedValidationContract()
    {
        return new WorkflowValidationContract
        {
            DeclaredUserOwnedFields = ["review.approved", "approval_decision", "approval_notes"],
            GovernanceEntry = new WorkflowGovernanceEntryContract(),
            Gates = new Dictionary<string, WorkflowValidationGate>(StringComparer.Ordinal)
            {
                ["gate.bootstrap_mcp_ready"] = new WorkflowValidationGate
                {
                    Description = "The MCP-first governance entry must be complete before governed work.",
                    PassExpression = "context.Has(\"mcp_startup_evidence\") && context.Get<bool>(\"mcp_startup_evidence.fragment_bounded\") == true",
                    RequiredOutputFamilies = ["mcp_startup_evidence"],
                    RequiredMachineReadableOutputFamilies = ["mcp_startup_evidence"],
                    ValueSemantics = new Dictionary<string, string>(StringComparer.Ordinal) { ["mcp_startup_evidence"] = "nonEmptyObject" },
                    InstanceBinding = "current_workflow_instance",
                    FailureGuidance = new WorkflowGateFailureGuidance
                    {
                        Summary = "The governance-entry fragment inspection is incomplete.",
                        NextAction = "Try MCP registration first using the runtime descriptor; if MCP is unavailable before dispatch, use the same descriptor for the allowed CLI backup and retry.",
                        EvidenceReferences = [new WorkflowEvidenceReference { Path = "tests/dotnet/Techne.Loom.SkillOrchestrator.Tests/SkillOrchestratorBehaviorTests.cs", StartLine = 1, EndLine = 1, Quote = "using Techne.Loom.Abstractions.TaskTracking.Model;" }],
                    },
                },
                ["gate.assessment"] = new WorkflowValidationGate
                {
                    Description = "Assessment deliverables gate.",
                    InstanceBinding = "current_workflow_instance",
                    PassExpression = "context.Has(\"assessment_summary_json\") && context.Has(\"assessment_report_md\")",
                    RequiredOutputFamilies = ["assessment_summary_json", "assessment_report_md"],
                    RequiredMachineReadableOutputFamilies = ["assessment_summary_json"],
                    RequiredHumanReviewableOutputFamilies = ["assessment_report_md"],
                    ValueSemantics = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["assessment_summary_json"] = "nonEmptyString",
                        ["assessment_report_md"] = "nonEmptyString",
                    },
                    FailureGuidance = new WorkflowGateFailureGuidance
                    {
                        Summary = "Assessment evidence is incomplete.",
                        NextAction = "Publish both assessment output families and retry the gate.",
                        EvidenceReferences = [new WorkflowEvidenceReference
                        {
                            Path = "tests/dotnet/Techne.Loom.SkillOrchestrator.Tests/SkillOrchestratorBehaviorTests.cs",
                            StartLine = 1,
                            EndLine = 1,
                            Quote = "using Techne.Loom.Abstractions.TaskTracking.Model;",
                        }],
                    },
                },
            },
            Routes = new Dictionary<string, WorkflowRouteValidationProfile>(StringComparer.Ordinal)
            {
                ["evaluation_only"] = new WorkflowRouteValidationProfile
                {
                    RequiredTerminalGateIds = ["gate.assessment"],
                },
            },
        };
    }

    protected static WorkflowInstance CreateContextWorkflow()
    {
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            WorkflowPhase = "Evaluation",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.check",
                    TransitionIds = ["transition.check"],
                },
            ],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };

        var done = new StateNode
        {
            Id = "state.done",
            Name = "Done",
            WorkflowPhase = "Done",
            Groups = [],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };

        var check = new ExpressionTransition
        {
            Id = "transition.check",
            Name = "Check context",
            TargetNodeId = done.Id,
            StepKind = WorkflowStepKind.ConditionBranch,
            SucceedExpression = "context.Get<bool>(\"review.approved\")",
            GuardExpression = "true",
            WorkflowPhase = "Evaluation",
        };

        return new WorkflowInstance
        {
            InstanceId = $"context-wf-{Guid.NewGuid():N}",
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = done.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [done.Id] = done,
                [check.Id] = check,
            },
            Context = new Dictionary<string, object?>(StringComparer.Ordinal),
        };
    }

    protected static WorkflowInstance CreateSelfLoopWorkflow()
    {
        var start = new StateNode
        {
            Id = "state.loop",
            Name = "Loop",
            WorkflowPhase = "Loop",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.loop",
                    TransitionIds = ["transition.loop"],
                },
            ],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };

        var loop = new ExpressionTransition
        {
            Id = "transition.loop",
            Name = "Loop forever",
            TargetNodeId = start.Id,
            StepKind = WorkflowStepKind.ConditionBranch,
            GuardExpression = "true",
            SucceedExpression = "true",
            WorkflowPhase = "Loop",
        };

        return new WorkflowInstance
        {
            InstanceId = $"self-loop-wf-{Guid.NewGuid():N}",
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [loop.Id] = loop,
            },
            Context = new Dictionary<string, object?>(StringComparer.Ordinal),
        };
    }

    protected static async Task<(int ExitCode, string StdOut, string StdErr)> RunCliAsync(string repoRoot, string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{GetCliAssemblyPath()}\" {arguments}",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start SO CLI process.");
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, stdout, stderr);
    }

    protected static void EnsureWorkflowPhases(WorkflowInstance workflow, string fallbackPhase)
    {
        foreach (var state in workflow.GetStateNodes().Values)
        {
            if (!string.IsNullOrWhiteSpace(state.WorkflowPhase))
            {
                continue;
            }

            state.WorkflowPhase = string.Equals(state.Id, workflow.EndNodeId, StringComparison.Ordinal)
                ? "Done"
                : fallbackPhase;
        }
    }

    protected static string ComputeCanonicalDocumentHash(string path)
    {
        var content = File.ReadAllText(path)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .TrimEnd();
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
    }

    protected static Dictionary<string, object?> CreateMermaidDeliveryEvidence()
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["status"] = "workspace_mirror",
            ["generation_status"] = "fresh",
            ["artifact_generated"] = true,
            ["link_resolvable"] = true,
        };
    }

    protected static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Techne.Loom.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }

    protected static string GetWorkflowPayloadPath(string repoRoot)
    {
        return Path.Combine(repoRoot, "tests", "dotnet", "Techne.Loom.SkillOrchestrator.Tests", "workflow.payload.json");
    }

    protected static string GetLoomSkillEnhancementRoot(string repoRoot)
    {
        var agentPath = Path.Combine(repoRoot, ".agents", "skills", "loom-skill-enhancement");
        if (Directory.Exists(agentPath))
        {
            return agentPath;
        }

        var githubPath = Path.Combine(repoRoot, ".github", "skills", "loom-skill-enhancement");
        if (Directory.Exists(githubPath))
        {
            return githubPath;
        }

        throw new DirectoryNotFoundException("Could not locate loom-skill-enhancement skill root in .agents/skills or .github/skills.");
    }

    protected static string CreateSkillRoot()
    {
        var skillRoot = Path.Combine(Path.GetTempPath(), $"techne-loom-so-skill-root-{Guid.NewGuid():N}");
        Directory.CreateDirectory(skillRoot);
        File.WriteAllText(Path.Combine(skillRoot, "SKILL.md"), "# Temp skill\n");
        return skillRoot;
    }

    protected static string GetCliAssemblyPath()
    {
        return typeof(DefaultWorkflowTaskTrackingService).Assembly.Location;
    }

    protected static string GetEscapedCommandName()
        => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd" : "bash";

    protected static string GetEscapedCommandArguments()
        => RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "/c (echo ^<danger^> & echo error-line 1>&2)"
            : "-lc \"printf '<danger>\\n'; printf 'error-line\\n' >&2\"";

    protected static string GetEscapedCommandPrefix()
        => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd /c (echo" : "bash -lc";

    protected static JsonDocument ReadSoEnvelope(string stdout)
    {
        return ReadSoEnvelopeByType(stdout, null);
    }

    protected static JsonDocument ReadFinalSoEnvelope(string stdout)
    {
        return ReadSoEnvelopeByType(stdout, type => !string.Equals(type, "progress", StringComparison.Ordinal));
    }

    protected static JsonDocument ReadSoEnvelopeByType(string stdout, Func<string, bool>? predicate)
    {
        const string startTag = "<so_property>";
        const string endTag = "</so_property>";
        var index = 0;
        JsonDocument? fallback = null;

        while (true)
        {
            var startIndex = stdout.IndexOf(startTag, index, StringComparison.Ordinal);
            if (startIndex < 0)
            {
                break;
            }

            var endIndex = stdout.IndexOf(endTag, startIndex, StringComparison.Ordinal);
            if (endIndex <= startIndex)
            {
                break;
            }

            var json = stdout.Substring(startIndex + startTag.Length, endIndex - startIndex - startTag.Length).Trim();
            var document = JsonDocument.Parse(json);
            fallback?.Dispose();
            fallback = document;

            var type = document.RootElement.GetProperty("type").GetString() ?? string.Empty;
            if (predicate is null || predicate(type))
            {
                return document;
            }

            index = endIndex + endTag.Length;
        }

        if (fallback is not null)
        {
            return fallback;
        }

        throw new InvalidOperationException("SO CLI output did not contain a so_property block.");
    }

    protected static void AssertMermaidStateGraphConnected(string mermaid, WorkflowInstance instance)
    {
        var stateIds = instance.Nodes.Values
            .OfType<StateNode>()
            .Select(static state => state.Id)
            .ToArray();

        Assert.NotEmpty(stateIds);

        var adjacency = stateIds.ToDictionary(
            static stateId => stateId,
            static _ => new HashSet<string>(StringComparer.Ordinal),
            StringComparer.Ordinal);

        foreach (var line in mermaid.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var match = Regex.Match(line, @"^(?<from>\S+)\s+-->\|.*\|\s+(?<to>\S+)$", RegexOptions.CultureInvariant);
            if (!match.Success)
            {
                continue;
            }

            var from = match.Groups["from"].Value;
            var to = match.Groups["to"].Value;
            if (!adjacency.ContainsKey(from) || !adjacency.ContainsKey(to))
            {
                continue;
            }

            adjacency[from].Add(to);
            adjacency[to].Add(from);
        }

        var visited = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>();
        queue.Enqueue(instance.StartNodeId);
        visited.Add(instance.StartNodeId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var next in adjacency[current])
            {
                if (visited.Add(next))
                {
                    queue.Enqueue(next);
                }
            }
        }

        var disconnectedStates = stateIds.Where(stateId => !visited.Contains(stateId)).ToArray();
        Assert.True(disconnectedStates.Length == 0, $"Mermaid graph contains disconnected states: {string.Join(", ", disconnectedStates)}");
    }

    protected static void AssertFileStartsWithMermaidFence(string filePath)
    {
        var bytes = File.ReadAllBytes(filePath);
        Assert.True(bytes.Length >= 3, $"Expected {filePath} to contain at least three bytes.");
        Assert.Equal((byte)'`', bytes[0]);
        Assert.Equal((byte)'`', bytes[1]);
        Assert.Equal((byte)'`', bytes[2]);
    }
}
