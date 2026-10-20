using System.Text.Json;
using System.Text.Json.Nodes;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class WorkflowPhaseCompilerTests
{
    [Fact]
    public void CompileChecksExpressionStaticallyWithoutInvokingIt()
    {
        var result = Compile(
            includeReviewImport: true,
            "p00_input.approved",
            "bool");

        Assert.Equal("passed", result.Status);
        Assert.True(result.StaticOnly);
        Assert.NotNull(result.Fragment);
        Assert.Empty(result.InterfaceChangeRequests);
    }

    [Fact]
    public void CompileRequestsInterfaceChangeForDeclaredPathOutsideFrozenImports()
    {
        var result = Compile(
            includeReviewImport: false,
            "p00_input.approved",
            "bool");

        Assert.Equal("interfaceChangeRequested", result.Status);
        var request = Assert.Single(result.InterfaceChangeRequests);
        Assert.Equal("addImport", request.Kind);
        Assert.Equal("p00_input.approved", request.ContextPath);
        Assert.Equal("p01_review.start", request.NodeId);
        Assert.Null(result.Fragment);
    }

    [Fact]
    public void CompileRequestsCatalogChangeForUnknownContextPath()
    {
        var result = Compile(
            includeReviewImport: true,
            "p00_input.missing",
            "bool");

        Assert.Equal("interfaceChangeRequested", result.Status);
        var request = Assert.Single(result.InterfaceChangeRequests);
        Assert.Equal("addContextField", request.Kind);
        Assert.Equal("p00_input.missing", request.ContextPath);
    }

    [Fact]
    public void CompileRejectsExpressionTypeMismatch()
    {
        var result = Compile(
            includeReviewImport: true,
            "p00_input.approved",
            "string");

        Assert.Equal("failed", result.Status);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == "LOOM.EXPR.CONTEXT.FIELD_TYPE_MISMATCH");
        Assert.Empty(result.InterfaceChangeRequests);
    }

    [Fact]
    public void CompileRequestsExportForProjectionOutsideFrozenInterface()
    {
        var result = Compile(
            includeReviewImport: true,
            "p00_input.approved",
            "bool",
            outputPath: "p01_review.result");

        Assert.Equal("interfaceChangeRequested", result.Status);
        var request = Assert.Single(result.InterfaceChangeRequests);
        Assert.Equal("addExport", request.Kind);
        Assert.Equal("p01_review.result", request.ContextPath);
        Assert.Equal("p01_review.done", request.NodeId);
    }

    [Fact]
    public void CompileRejectsUndeclaredCrossPhaseTarget()
    {
        var result = Compile(
            includeReviewImport: true,
            "p00_input.approved",
            "bool",
            targetNodeId: "p02_other.start");

        Assert.Equal("failed", result.Status);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == "LOOM.PHASE.ROUTE_UNDECLARED");
    }

    [Fact]
    public void CompileRejectsTerminalRouteOwnershipMismatch()
    {
        var result = Compile(
            includeReviewImport: true,
            path: "p00_input.approved",
            consumerType: "bool",
            terminalRoutes: []);

        Assert.Equal("failed", result.Status);
        Assert.Contains(result.Diagnostics, static diagnostic =>
            diagnostic.Code == "LOOM.PHASE.TERMINAL_ROUTES_MISMATCH");
    }

    [Fact]
    public void CompileRejectsDeclaredRoutePointingToLocalNode()
    {
        var result = Compile(
            includeReviewImport: true,
            path: "p00_input.approved",
            consumerType: "bool",
            targetNodeId: "p01_review.start",
            includeOutgoingBackedge: true);

        Assert.Equal("failed", result.Status);
        Assert.Contains(result.Diagnostics, static diagnostic =>
            diagnostic.Code == "LOOM.PHASE.ROUTE_TARGET_CONFLICT");
    }

    [Fact]
    public void CompileRequestsImportForPlanInputPath()
    {
        var result = CompileWithCommand(
            new CommandInvocation(),
            plan: new PlanStepContract { InputPaths = ["p00_input.revision"] },
            stepKind: WorkflowStepKind.Plan);

        Assert.Equal("interfaceChangeRequested", result.Status);
        var request = Assert.Single(result.InterfaceChangeRequests, static item => item.Kind == "addImport");
        Assert.Equal("p00_input.revision", request.ContextPath);
    }

    [Fact]
    public void CompileRequestsCatalogChangeForUnknownPlanInputPath()
    {
        var result = CompileWithCommand(
            new CommandInvocation(),
            plan: new PlanStepContract { InputPaths = ["p00_input.missing"] },
            stepKind: WorkflowStepKind.Plan);

        Assert.Equal("interfaceChangeRequested", result.Status);
        var request = Assert.Single(result.InterfaceChangeRequests, static item => item.Kind == "addContextField");
        Assert.Equal("p00_input.missing", request.ContextPath);
    }

    [Fact]
    public void CompileRequestsCatalogChangeForUnknownContextRequiredInput()
    {
        var result = CompileWithCommand(new CommandInvocation
        {
            Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["requiredInputs"] = new object?[] { "p00_input.missing" },
            },
        });

        Assert.Equal("interfaceChangeRequested", result.Status);
        var request = Assert.Single(result.InterfaceChangeRequests, static item => item.Kind == "addContextField");
        Assert.Equal("p00_input.missing", request.ContextPath);
    }

    [Fact]
    public void CompileRequestsImportForContextRequiredInput()
    {
        var result = CompileWithCommand(new CommandInvocation
        {
            Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["requiredInputs"] = new object?[] { "p00_input.revision" },
            },
        });

        Assert.Equal("interfaceChangeRequested", result.Status);
        var request = Assert.Single(result.InterfaceChangeRequests, static item => item.Kind == "addImport");
        Assert.Equal("p00_input.revision", request.ContextPath);
    }

    [Theory]
    [InlineData("updates")]
    [InlineData("outputBindings")]
    public void CompileRequestsImportForContextProjectionSource(string parameterName)
    {
        var result = CompileWithCommand(new CommandInvocation
        {
            Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [parameterName] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["p01_review.result"] = "$context:p00_input.revision",
                },
            },
        });

        Assert.Contains(result.InterfaceChangeRequests, static request =>
            request.Kind == "addImport" && request.ContextPath == "p00_input.revision");
    }

    [Theory]
    [InlineData("updates")]
    [InlineData("outputBindings")]
    public void CompileRequestsCatalogChangeForUnknownContextProjectionSource(string parameterName)
    {
        var result = CompileWithCommand(new CommandInvocation
        {
            Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [parameterName] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["p01_review.result"] = "$context:p00_input.missing",
                },
            },
        });

        Assert.Equal("interfaceChangeRequested", result.Status);
        var request = Assert.Single(result.InterfaceChangeRequests, static item => item.Kind == "addContextField");
        Assert.Equal("p00_input.missing", request.ContextPath);
    }

    [Fact]
    public void CompileDoesNotTreatPayloadProjectionAsContextImport()
    {
        var result = CompileWithCommand(
            new CommandInvocation
            {
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["requiredInputs"] = new object?[] { "p00_input.revision" },
                    ["outputBindings"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["p01_review.result"] = "$payload:p00_input.revision",
                    },
                },
            },
            stepKind: WorkflowStepKind.ModelThink);

        Assert.DoesNotContain(result.InterfaceChangeRequests, static request => request.Kind == "addImport");
        Assert.DoesNotContain(result.Diagnostics, static diagnostic =>
            diagnostic.Code == "LOOM.CONTEXT.PROJECTION.SOURCE_UNSUPPORTED");
    }

    private static WorkflowPhaseCompileResult CompileWithCommand(
        CommandInvocation command,
        PlanStepContract? plan = null,
        WorkflowStepKind? stepKind = null)
        => Compile(
            includeReviewImport: true,
            path: "p00_input.approved",
            consumerType: "bool",
            command: command,
            plan: plan,
            stepKind: stepKind);

    private static WorkflowPhaseCompileResult Compile(
        bool includeReviewImport,
        string path,
        string consumerType,
        string? outputPath = null,
        string? targetNodeId = null,
        List<string>? terminalRoutes = null,
        bool includeOutgoingBackedge = false,
        CommandInvocation? command = null,
        PlanStepContract? plan = null,
        WorkflowStepKind? stepKind = null)
    {
        var expression = new ExpressionDefinition
        {
            Source = $"context.Get<{consumerType}>(\"{path}\")" + (consumerType == "string" ? " == \"approved\"" : string.Empty),
        };
        return WorkflowPhaseCompiler.Compile(
            Skeleton(includeReviewImport, includeOutgoingBackedge),
            "p01_review",
            Fragment(expression, outputPath, targetNodeId, terminalRoutes, command, plan, stepKind),
            new ExpressionBinding
            {
                RequiredExpressionCapabilities = [ExpressionCapabilityIds.ContextGet],
            },
            Catalog(),
            validation: null,
            "phase-fragment.json");
    }

    private static WorkflowPhaseFragment Fragment(
        ExpressionDefinition expression,
        string? outputPath = null,
        string? targetNodeId = null,
        List<string>? terminalRoutes = null,
        CommandInvocation? command = null,
        PlanStepContract? plan = null,
        WorkflowStepKind? stepKind = null)
        => WorkflowPhaseFragment.Create(
            "phase-compile",
            "p01_review",
            new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                ["p01_review.start"] = new StateNode
                {
                    Id = "p01_review.start",
                    Name = "Review input",
                    StateFailedExpression = expression,
                    Groups = [new TransitionGroup { Id = "review", TransitionIds = ["p01_review.done"] }],
                },
                ["p01_review.done"] = new CommandTransition
                {
                    Id = "p01_review.done",
                    Name = "Complete review",
                    TargetNodeId = targetNodeId,
                    OutputPath = outputPath,
                    TerminalRoutes = terminalRoutes ?? ["completed"],
                    Command = command ?? new CommandInvocation(),
                    Plan = plan,
                    StepKind = stepKind ?? WorkflowStepKind.ModelThink,
                },
            });

    private static WorkflowPhaseSkeleton Skeleton(bool includeReviewImport, bool includeOutgoingBackedge = false)
    {
        var root = JsonNode.Parse("""
            {
              "schemaVersion": "loom.workflow-skeleton.v1",
              "workflowId": "phase-compile",
              "phases": [
                {
                  "id": "p00_input", "prefix": "p00_input.", "mode": "phaseSliced",
                  "entries": ["p00_input.start"], "exits": ["p00_input.done"],
                  "imports": [], "exports": ["p00_input.approved"],
                  "dependencies": [], "gateIds": [], "terminalRoutes": []
                },
                {
                  "id": "p01_review", "prefix": "p01_review.", "mode": "phaseSliced",
                  "entries": ["p01_review.start"], "exits": ["p01_review.done"],
                  "imports": [], "exports": [],
                  "dependencies": ["p00_input"], "gateIds": [], "terminalRoutes": ["completed"]
                }
              ],
              "routes": [
                {"fromPhaseId":"p00_input","fromExitId":"p00_input.done","toPhaseId":"p01_review","toEntryId":"p01_review.start"}
              ],
              "backedges": []
            }
            """)!.AsObject();
        if (includeReviewImport)
        {
            root["phases"]!.AsArray()[1]!["imports"]!.AsArray().Add("p00_input.approved");
        }

        if (includeOutgoingBackedge)
        {
            root["backedges"]!.AsArray().Add(JsonNode.Parse("""
                {"fromPhaseId":"p01_review","fromExitId":"p01_review.done","toPhaseId":"p00_input","toEntryId":"p00_input.start"}
                """));
        }

        using var document = JsonDocument.Parse(root.ToJsonString());
        return WorkflowPhaseSkeleton.Parse(document.RootElement, "skeleton.json");
    }

    private static ContextFieldCatalog Catalog()
    {
        using var document = JsonDocument.Parse("""
            {
              "context_fields": {
                "schemaVersion": "loom.context-fields.v1",
                "fields": [
                  {"path":"p00_input.approved","type":"boolean","nullable":false,"owner":"agent","description":"Approval decision."},
                  {"path":"p00_input.revision","type":"string","nullable":false,"owner":"agent","description":"Revision input."},
                  {"path":"p01_review.result","type":"string","nullable":true,"owner":"agent","description":"Review output."}
                ]
              }
            }
            """);
        return ContextFieldCatalog.Parse(document.RootElement, "contract.json");
    }
}
