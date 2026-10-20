using System.Text.Json;
using System.Text.Json.Nodes;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;
using Techne.Loom.Tests.Shared;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class WorkflowPhaseMergeTests
{
    [Fact]
    public void MergeIsDeterministicAndDoesNotMutateInputs()
    {
        var skeleton = ParseSkeleton();
        var template = DesignTemplate();
        var results = new[] { Result("p00_input"), Result("p01_review") };
        var templateBefore = WorkflowJsonSerializer.Serialize(template);

        var first = WorkflowPhaseMerger.Merge(template, skeleton, Catalog(), results);
        var second = WorkflowPhaseMerger.Merge(template, skeleton, Catalog(), results.Reverse().ToArray());

        Assert.Equal(WorkflowJsonSerializer.Serialize(first), WorkflowJsonSerializer.Serialize(second));
        Assert.Equal(templateBefore, WorkflowJsonSerializer.Serialize(template));
        Assert.Equal("p00_input.start", first.CurrentNodeId);
        Assert.Equal("p01_review.start", Assert.IsType<CommandTransition>(first.Nodes["p00_input.done"]).TargetNodeId);
        Assert.Null(Assert.IsType<CommandTransition>(results[0].Fragment!.Nodes["p00_input.done"]).TargetNodeId);
        Assert.Equal(first.Nodes.Keys.OrderBy(static id => id, StringComparer.Ordinal), first.Nodes.Keys);
    }

    [Theory]
    [InlineData(40, 2)]
    [InlineData(500, 20)]
    public void PhaseFixtureValidatesThresholdAndOversizedPartitioning(int authoredStateCount, int expectedPhaseCount)
    {
        var fixture = WorkflowPhaseAcceptanceFixture.Create(authoredStateCount);
        var validation = WorkflowPhaseSkeletonValidator.Validate(fixture.Skeleton, fixture.Catalog, "phase-acceptance-skeleton.json");

        Assert.True(validation.IsValid, string.Join(Environment.NewLine, validation.Diagnostics.Select(static diagnostic => diagnostic.Message)));
        Assert.Equal(expectedPhaseCount, fixture.Skeleton.Phases.Count);
        Assert.Equal(expectedPhaseCount - 1, fixture.Skeleton.Routes.Count);
        Assert.Equal(authoredStateCount, fixture.Fragments.Sum(static fragment =>
            fragment.Nodes.Values.OfType<StateNode>().Count()));
        Assert.All(fixture.Skeleton.Phases, static phase => Assert.Equal("phaseSliced", phase.Mode));
        Assert.All(fixture.Fragments, static fragment =>
            Assert.InRange(fragment.Nodes.Values.OfType<StateNode>().Count(), 1, 25));
    }

    [Fact]
    public void FragmentAndResultRoundTripAsVersionedJson()
    {
        var result = Result("p00_input");

        var fragment = WorkflowPhaseFragment.Parse(result.Fragment!.ToJson(), "fragment.json");
        var parsedResult = WorkflowPhaseCompileResult.Parse(result.ToJson(), "result.json");

        Assert.Equal(WorkflowPhaseFragment.CurrentSchemaVersion, fragment.SchemaVersion);
        Assert.Equal(result.WorkflowId, parsedResult.WorkflowId);
        Assert.Equal(result.PhaseId, parsedResult.Fragment!.PhaseId);
        Assert.True(parsedResult.StaticOnly);
    }

    [Fact]
    public void MergeRejectsMissingAndDuplicatePhaseResults()
    {
        var skeleton = ParseSkeleton();
        var template = DesignTemplate();

        var missing = Assert.Throws<InvalidOperationException>(() =>
            WorkflowPhaseMerger.Merge(template, skeleton, Catalog(), [Result("p00_input")]));
        var duplicate = Assert.Throws<InvalidOperationException>(() =>
            WorkflowPhaseMerger.Merge(template, skeleton, Catalog(), [Result("p00_input"), Result("p00_input")]));

        Assert.Contains("exactly one result", missing.Message, StringComparison.Ordinal);
        Assert.Contains("duplicate result", duplicate.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MergeRejectsResultCompiledAgainstOutdatedInterface()
    {
        var originalSkeleton = ParseSkeleton();
        var currentSkeleton = ParseSkeleton(includeReviewImport: false);
        var catalog = Catalog();
        var expressionBinding = new ExpressionBinding
        {
            RequiredExpressionCapabilities = [ExpressionCapabilityIds.ContextGet],
        };
        var fragment = WorkflowPhaseFragment.Create(
            "intake-review",
            "p01_review",
            new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                ["p01_review.start"] = new StateNode
                {
                    Id = "p01_review.start",
                    Name = "Review input",
                    StateFailedExpression = new ExpressionDefinition
                    {
                        Source = "context.Get<string>(\"p00_input.request\") != null",
                    },
                    Groups = [new TransitionGroup { Id = "review", TransitionIds = ["p01_review.done"] }],
                },
                ["p01_review.done"] = new CommandTransition
                {
                    Id = "p01_review.done",
                    Name = "Finish",
                    TerminalRoutes = ["done"],
                },
            });
        var staleResult = WorkflowPhaseCompiler.Compile(
            originalSkeleton,
            "p01_review",
            fragment,
            expressionBinding,
            catalog,
            validation: null,
            sourcePath: "previous-phase.json");
        Assert.Equal("passed", staleResult.Status);

        var template = DesignTemplate();
        template.ExpressionBinding = expressionBinding;
        var error = Assert.Throws<InvalidOperationException>(() =>
            WorkflowPhaseMerger.Merge(template, currentSkeleton, catalog, [Result("p00_input"), staleResult]));

        Assert.Contains("static revalidation", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MergeRejectsInterfaceChangeRequestsAndActiveWorkflowInstances()
    {
        var skeleton = ParseSkeleton();
        var interfaceChange = Result("p01_review") with
        {
            Status = "interfaceChangeRequested",
            InterfaceChangeRequests = [new WorkflowPhaseInterfaceChangeRequest("addImport", "A context path is missing.", "review.notes")],
            Fragment = null,
        };
        var interfaceError = Assert.Throws<InvalidOperationException>(() =>
            WorkflowPhaseMerger.Merge(DesignTemplate(), skeleton, Catalog(), [Result("p00_input"), interfaceChange]));

        var activeTemplate = DesignTemplate();
        activeTemplate.Status = WorkflowStatus.Running;
        var activeError = Assert.Throws<InvalidOperationException>(() =>
            WorkflowPhaseMerger.Merge(activeTemplate, skeleton, Catalog(), [Result("p00_input"), Result("p01_review")]));

        Assert.Contains("interfaceChangeRequested", interfaceError.Message, StringComparison.Ordinal);
        Assert.Contains("design template only", activeError.Message, StringComparison.Ordinal);
    }

    private static WorkflowPhaseCompileResult Result(string phaseId)
    {
        var nodes = phaseId switch
        {
            "p00_input" => new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                ["p00_input.start"] = new StateNode
                {
                    Id = "p00_input.start",
                    Name = "Capture input",
                    Groups = [new TransitionGroup { Id = "input-route", TransitionIds = ["p00_input.done"] }],
                },
                ["p00_input.done"] = new CommandTransition { Id = "p00_input.done", Name = "Continue" },
            },
            "p01_review" => new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                ["p01_review.start"] = new StateNode
                {
                    Id = "p01_review.start",
                    Name = "Review input",
                    Groups = [new TransitionGroup { Id = "review-terminal", TransitionIds = ["p01_review.done"] }],
                },
                ["p01_review.done"] = new CommandTransition { Id = "p01_review.done", Name = "Finish", TerminalRoutes = ["done"] },
            },
            _ => throw new ArgumentOutOfRangeException(nameof(phaseId)),
        };
        var fragment = WorkflowPhaseFragment.Create("intake-review", phaseId, nodes);
        return new WorkflowPhaseCompileResult(
            WorkflowPhaseCompileResult.CurrentSchemaVersion,
            "intake-review",
            WorkflowPhaseSkeleton.CurrentSchemaVersion,
            phaseId,
            "passed",
            true,
            WorkflowPhaseCompileResult.GetSyntheticBoundaryIds(phaseId),
            [],
            [],
            fragment);
    }

    private static WorkflowInstance DesignTemplate()
        => new()
        {
            InstanceId = "template-intake-review",
            StartNodeId = "p00_input.start",
            CurrentNodeId = "template-current-node",
            Status = WorkflowStatus.ReadyToStart,
        };

    private static WorkflowPhaseSkeleton ParseSkeleton(bool includeReviewImport = true)
    {
        var root = JsonNode.Parse("""
            {
              "schemaVersion": "loom.workflow-skeleton.v1",
              "workflowId": "intake-review",
              "phases": [
                {
                  "id": "p00_input", "prefix": "p00_input.", "mode": "phaseSliced",
                  "entries": ["p00_input.start"], "exits": ["p00_input.done"],
                  "imports": ["p00_input.request"], "exports": ["p00_input.request"],
                  "dependencies": [], "gateIds": [], "terminalRoutes": []
                },
                {
                  "id": "p01_review", "prefix": "p01_review.", "mode": "phaseSliced",
                  "entries": ["p01_review.start"], "exits": ["p01_review.done"],
                  "imports": ["p00_input.request"], "exports": ["review.approved"],
                  "dependencies": ["p00_input"], "gateIds": [], "terminalRoutes": ["done"]
                }
              ],
              "routes": [
                {"fromPhaseId":"p00_input","fromExitId":"p00_input.done","toPhaseId":"p01_review","toEntryId":"p01_review.start"}
              ],
              "backedges": []
            }
            """)!.AsObject();
        if (!includeReviewImport)
        {
            root["phases"]!.AsArray()[1]!["imports"]!.AsArray().Clear();
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
                  {"path":"p00_input.request","type":"string","nullable":false,"owner":"caller","description":"Request."},
                  {"path":"review.approved","type":"boolean","nullable":false,"owner":"agent","description":"Decision."}
                ]
              }
            }
            """);
        return ContextFieldCatalog.Parse(document.RootElement, "contract.json");
    }
}
