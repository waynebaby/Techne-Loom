using System.Text.Json;
using System.Text.Json.Nodes;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class WorkflowPhaseImpactAnalyzerTests
{
    [Fact]
    public void AuthoredChangeInvalidatesOnlyItsDeclaredDependents()
    {
        var previous = Snapshot(Skeleton(), Catalog(), [Result("p00_input"), Result("p01_review"), Result("p02_other")]);
        var candidate = Snapshot(Skeleton(), Catalog(), []);

        var result = WorkflowPhaseImpactAnalyzer.Analyze(previous, candidate, ["p00_input"]);

        Assert.Equal(["p00_input", "p01_review"], result.RevalidatePhaseIds);
        Assert.Equal(["p02_other"], result.ReusablePhaseIds);
        Assert.False(result.FullRevalidationRequired);
        Assert.True(result.FullWorkflowCompileRequired);
        Assert.Contains(result.Reasons, static reason => reason.Code == "LOOM.PHASE.IMPACT.DEPENDENT_INVALIDATED");
    }

    [Fact]
    public void CatalogFactChangeInvalidatesProducerAndConsumerPhases()
    {
        var previous = Snapshot(Skeleton(), Catalog("string"), [Result("p00_input"), Result("p01_review"), Result("p02_other")]);
        var candidate = Snapshot(Skeleton(), Catalog("number"), []);

        var result = WorkflowPhaseImpactAnalyzer.Analyze(previous, candidate, []);

        Assert.Equal(["p00_input", "p01_review"], result.RevalidatePhaseIds);
        Assert.Equal(["p02_other"], result.ReusablePhaseIds);
        Assert.Contains(result.Reasons, static reason => reason.Code == "LOOM.PHASE.IMPACT.CONTEXT_FIELD_CHANGED");
    }

    [Theory]
    [InlineData("plan")]
    [InlineData("requiredInputs")]
    [InlineData("updates")]
    [InlineData("outputBindings")]
    public void CommandContextReadUsageInvalidatesPhaseWhenCatalogFieldChanges(string readKind)
    {
        var skeleton = Skeleton(includeOtherPhaseImport: true);
        var previousCatalog = Catalog();
        var command = new CommandInvocation();
        PlanStepContract? plan = null;
        if (readKind == "plan")
        {
            plan = new PlanStepContract { InputPaths = ["p00_input.request"] };
        }
        else if (readKind == "requiredInputs")
        {
            command.Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["requiredInputs"] = new object?[] { "p00_input.request" },
            };
        }
        else
        {
            command.Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [readKind] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["other.notes"] = "$context:p00_input.request",
                },
            };
        }

        var fragment = WorkflowPhaseFragment.Create(
            "phase-impact",
            "p02_other",
            new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                ["p02_other.start"] = new StateNode
                {
                    Id = "p02_other.start",
                    Name = "Review input",
                    Groups = [new TransitionGroup { Id = "main", TransitionIds = ["p02_other.done"] }],
                },
                ["p02_other.done"] = new CommandTransition
                {
                    Id = "p02_other.done",
                    Name = "Read request",
                    Command = command,
                    Plan = plan,
                    StepKind = readKind == "plan" ? WorkflowStepKind.Plan : WorkflowStepKind.ModelThink,
                    TerminalRoutes = ["done", "failed"],
                },
            });
        var commandResult = WorkflowPhaseCompiler.Compile(
            skeleton,
            "p02_other",
            fragment,
            new ExpressionBinding(),
            previousCatalog,
            validation: null,
            sourcePath: "other-phase.json");
        Assert.Equal("passed", commandResult.Status);

        var previous = Snapshot(
            skeleton,
            previousCatalog,
            [Result("p00_input"), Result("p01_review"), commandResult]);
        var candidate = Snapshot(skeleton, Catalog("number"), []);

        var result = WorkflowPhaseImpactAnalyzer.Analyze(previous, candidate, []);

        Assert.Contains("p02_other", result.RevalidatePhaseIds);
        Assert.Contains(result.Reasons, static reason =>
            reason.Code == "LOOM.PHASE.IMPACT.CONTEXT_FIELD_USED"
            && reason.PhaseId == "p02_other"
            && reason.ContextPath == "p00_input.request");
    }

    [Fact]
    public void StructuredFragmentChangeIsDetectedWithoutHashes()
    {
        var previous = Snapshot(Skeleton(), Catalog(), [Result("p00_input"), Result("p01_review"), Result("p02_other")]);
        var candidate = Snapshot(Skeleton(), Catalog(), [Result("p02_other", "Updated" )]);

        var result = WorkflowPhaseImpactAnalyzer.Analyze(previous, candidate, []);

        Assert.Equal(["p02_other"], result.RevalidatePhaseIds);
        Assert.Equal(["p00_input", "p01_review"], result.ReusablePhaseIds);
        Assert.Contains(result.Reasons, static reason => reason.Code == "LOOM.PHASE.IMPACT.FRAGMENT_CHANGED");
    }

    [Fact]
    public void MissingCandidateEvidenceRequiresFullRevalidation()
    {
        var previous = Snapshot(Skeleton(), Catalog(), [Result("p00_input"), Result("p01_review"), Result("p02_other")]);
        var candidate = Snapshot(Skeleton(), Catalog(), []);

        var result = WorkflowPhaseImpactAnalyzer.Analyze(previous, candidate, []);

        Assert.Equal(["p00_input", "p01_review", "p02_other"], result.RevalidatePhaseIds);
        Assert.Empty(result.ReusablePhaseIds);
        Assert.True(result.FullRevalidationRequired);
        Assert.Contains(result.Reasons, static reason => reason.Code == "LOOM.PHASE.IMPACT.CANDIDATE_EVIDENCE_MISSING");
    }

    [Fact]
    public void MissingCandidateEvidenceDoesNotReusePriorResultsWhenRouteOrderChanges()
    {
        var previousSkeleton = SkeletonWithTwoRoutes(reverseRoutes: false);
        var candidateSkeleton = SkeletonWithTwoRoutes(reverseRoutes: true);
        Assert.True(WorkflowPhaseSkeletonValidator.Validate(previousSkeleton, Catalog(), "previous.json").IsValid);
        Assert.True(WorkflowPhaseSkeletonValidator.Validate(candidateSkeleton, Catalog(), "candidate.json").IsValid);
        var previous = Snapshot(previousSkeleton, Catalog(), [Result("p00_input"), Result("p01_review")]);
        var candidate = Snapshot(candidateSkeleton, Catalog(), []);

        var result = WorkflowPhaseImpactAnalyzer.Analyze(previous, candidate, []);

        Assert.Equal(["p00_input", "p01_review"], result.RevalidatePhaseIds);
        Assert.Empty(result.ReusablePhaseIds);
        Assert.True(result.FullRevalidationRequired);
    }

    [Fact]
    public void IncompatiblePriorResultIsNotReusedWithPartialCandidateResults()
    {
        var incompatibleResult = Result("p01_review");
        var nodes = incompatibleResult.Fragment!.Nodes.ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal);
        var transition = Assert.IsType<CommandTransition>(nodes["p01_review.done"]);
        nodes[transition.Id] = transition with { TerminalRoutes = ["stale-route"] };
        incompatibleResult = incompatibleResult with
        {
            Fragment = incompatibleResult.Fragment! with { Nodes = nodes },
        };
        var previous = Snapshot(
            Skeleton(),
            Catalog(),
            [Result("p00_input"), incompatibleResult, Result("p02_other")]);
        var candidate = Snapshot(Skeleton(), Catalog(), [Result("p02_other")]);

        var result = WorkflowPhaseImpactAnalyzer.Analyze(previous, candidate, []);

        Assert.Equal(["p01_review"], result.RevalidatePhaseIds);
        Assert.Equal(["p00_input", "p02_other"], result.ReusablePhaseIds);
        Assert.Contains(result.Reasons, static reason =>
            reason.Code == "LOOM.PHASE.IMPACT.RESULT_INCOMPATIBLE" && reason.PhaseId == "p01_review");
    }

    [Fact]
    public void PriorResultMustRemainCompatibleWithCandidateInterfaceToBeReused()
    {
        var previousSkeleton = Skeleton();
        var candidateSkeleton = Skeleton(includeReviewImport: false);
        var catalog = Catalog();
        var expressionBinding = new ExpressionBinding
        {
            RequiredExpressionCapabilities = [ExpressionCapabilityIds.ContextGet],
        };
        var priorReviewResult = CompileReviewResult(previousSkeleton, catalog, expressionBinding);
        Assert.Equal("passed", priorReviewResult.Status);
        var previous = Snapshot(
            previousSkeleton,
            catalog,
            [Result("p00_input"), priorReviewResult, Result("p02_other")],
            expressionBinding);
        var candidate = Snapshot(
            candidateSkeleton,
            catalog,
            [Result("p02_other")],
            expressionBinding);

        var result = WorkflowPhaseImpactAnalyzer.Analyze(previous, candidate, []);

        Assert.Equal(["p01_review"], result.RevalidatePhaseIds);
        Assert.Equal(["p00_input", "p02_other"], result.ReusablePhaseIds);
        Assert.Contains(result.Reasons, static reason =>
            reason.Code == "LOOM.PHASE.IMPACT.PREVIOUS_RESULT_INCOMPATIBLE" && reason.PhaseId == "p01_review");
    }


    [Fact]
    public void MissingPriorResultInvalidatesItsPhaseAndUnknownChangeExpandsAll()
    {
        var previous = Snapshot(Skeleton(), Catalog(), [Result("p00_input"), Result("p01_review")]);
        var candidate = Snapshot(Skeleton(), Catalog(), [Result("p00_input")]);

        var missing = WorkflowPhaseImpactAnalyzer.Analyze(previous, candidate, []);
        var unknown = WorkflowPhaseImpactAnalyzer.Analyze(previous, Snapshot(Skeleton(), Catalog(), []), ["unrecognized"]);

        Assert.Equal(["p02_other"], missing.RevalidatePhaseIds);
        Assert.Equal(["p00_input", "p01_review"], missing.ReusablePhaseIds);
        Assert.Equal(["p00_input", "p01_review", "p02_other"], unknown.RevalidatePhaseIds);
        Assert.True(unknown.FullRevalidationRequired);
    }

    [Fact]
    public void InvalidPhaseIdsFailClosedWithoutThrowing()
    {
        var allResults = new[] { Result("p00_input"), Result("p01_review"), Result("p02_other") };
        var candidate = Snapshot(Skeleton(), Catalog(), []);
        var invalidPrevious = Snapshot(
            Skeleton(),
            Catalog(),
            [Result("p00_input") with { PhaseId = null! }, Result("p01_review"), Result("p02_other")]);

        var invalidResult = WorkflowPhaseImpactAnalyzer.Analyze(invalidPrevious, candidate, []);
        var invalidChange = WorkflowPhaseImpactAnalyzer.Analyze(Snapshot(Skeleton(), Catalog(), allResults), candidate, [null!]);

        Assert.Equal(["p00_input", "p01_review", "p02_other"], invalidResult.RevalidatePhaseIds);
        Assert.True(invalidResult.FullRevalidationRequired);
        Assert.Equal(["p00_input", "p01_review", "p02_other"], invalidChange.RevalidatePhaseIds);
        Assert.True(invalidChange.FullRevalidationRequired);
    }

    [Fact]
    public void ImpactAnalysisRoundTripsAsVersionedJson()
    {
        var snapshot = Snapshot(Skeleton(), Catalog(), [Result("p00_input"), Result("p01_review"), Result("p02_other")]);
        var analysis = WorkflowPhaseImpactAnalyzer.Analyze(snapshot, snapshot with { PhaseResults = [] }, []);

        var parsed = WorkflowPhaseImpactAnalysis.Parse(analysis.ToJson(), "impact.json");

        Assert.Equal(WorkflowPhaseImpactAnalysis.CurrentSchemaVersion, parsed.SchemaVersion);
        Assert.Equal(analysis.WorkflowId, parsed.WorkflowId);
        Assert.Equal(analysis.RevalidatePhaseIds, parsed.RevalidatePhaseIds);
        Assert.Equal(analysis.ReusablePhaseIds, parsed.ReusablePhaseIds);
        Assert.True(parsed.FullWorkflowCompileRequired);
    }

    [Fact]
    public void RemovedPhaseIgnoresItsStaleCandidateResult()
    {
        var previous = Snapshot(Skeleton(), Catalog(), [Result("p00_input"), Result("p01_review"), Result("p02_other")]);
        var candidate = Snapshot(SkeletonWithoutOther(), Catalog(), [Result("p00_input"), Result("p01_review"), Result("p02_other")]);

        var result = WorkflowPhaseImpactAnalyzer.Analyze(previous, candidate, []);

        Assert.Empty(result.RevalidatePhaseIds);
        Assert.Equal(["p00_input", "p01_review"], result.ReusablePhaseIds);
        Assert.Equal(["p02_other"], result.RemovedPhaseIds);
        Assert.False(result.FullRevalidationRequired);
    }

    [Fact]
    public void WorkflowIdentityChangeRequiresFullRevalidationWithoutThrowing()
    {
        var previous = Snapshot(Skeleton(), Catalog(), [Result("p00_input"), Result("p01_review"), Result("p02_other")]);
        var candidate = Snapshot(Skeleton(workflowId: "phase-impact-renamed"), Catalog(), []);

        var result = WorkflowPhaseImpactAnalyzer.Analyze(previous, candidate, []);

        Assert.Equal("phase-impact-renamed", result.WorkflowId);
        Assert.Equal(["p00_input", "p01_review", "p02_other"], result.RevalidatePhaseIds);
        Assert.Empty(result.ReusablePhaseIds);
        Assert.True(result.FullRevalidationRequired);
        Assert.Contains(result.Reasons, static reason => reason.Code == "LOOM.PHASE.IMPACT.WORKFLOW_CHANGED");
    }

    [Fact]
    public void TerminalRouteOrderDoesNotInvalidatePassingCandidateResults()
    {
        var phaseResults = new[] { Result("p00_input"), Result("p01_review"), Result("p02_other") };
        var previous = Snapshot(Skeleton(), Catalog(), phaseResults);
        var candidate = Snapshot(Skeleton(reverseTerminalRoutes: true), Catalog(), phaseResults);

        var result = WorkflowPhaseImpactAnalyzer.Analyze(previous, candidate, []);

        Assert.Empty(result.RevalidatePhaseIds);
        Assert.Equal(["p00_input", "p01_review", "p02_other"], result.ReusablePhaseIds);
        Assert.False(result.FullRevalidationRequired);
        Assert.DoesNotContain(result.Reasons, static reason =>
            reason.Code == "LOOM.PHASE.IMPACT.PHASE_DEFINITION_CHANGED");
    }

    private static WorkflowPhaseImpactSnapshot Snapshot(
        WorkflowPhaseSkeleton skeleton,
        ContextFieldCatalog catalog,
        IReadOnlyCollection<WorkflowPhaseCompileResult> results,
        ExpressionBinding? expressionBinding = null)
        => new(skeleton, catalog, expressionBinding ?? new ExpressionBinding(), null, results);

    private static WorkflowPhaseCompileResult CompileReviewResult(
        WorkflowPhaseSkeleton skeleton,
        ContextFieldCatalog catalog,
        ExpressionBinding expressionBinding)
    {
        var fragment = WorkflowPhaseFragment.Create(
            "phase-impact",
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
                    Name = "Complete review",
                },
            });
        return WorkflowPhaseCompiler.Compile(
            skeleton,
            "p01_review",
            fragment,
            expressionBinding,
            catalog,
            validation: null,
            sourcePath: "review-phase.json");
    }

    private static WorkflowPhaseCompileResult Result(string phaseId, string transitionName = "Transition")
    {
        var prefix = phaseId switch
        {
            "p00_input" => "Input",
            "p01_review" => "Review",
            "p02_other" => "Other",
            _ => throw new ArgumentOutOfRangeException(nameof(phaseId)),
        };
        var fragment = WorkflowPhaseFragment.Create(
            "phase-impact",
            phaseId,
            new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [$"{phaseId}.start"] = new StateNode
                {
                    Id = $"{phaseId}.start",
                    Name = $"{prefix} start",
                    Groups = [new TransitionGroup { Id = "main", TransitionIds = [$"{phaseId}.done"] }],
                },
                [$"{phaseId}.done"] = new CommandTransition
                {
                    Id = $"{phaseId}.done",
                    Name = transitionName,
                    TerminalRoutes = phaseId == "p02_other" ? ["done", "failed"] : null,
                },
            });
        return new WorkflowPhaseCompileResult(
            WorkflowPhaseCompileResult.CurrentSchemaVersion,
            "phase-impact",
            WorkflowPhaseSkeleton.CurrentSchemaVersion,
            phaseId,
            "passed",
            true,
            WorkflowPhaseCompileResult.GetSyntheticBoundaryIds(phaseId),
            [],
            [],
            fragment);
    }

    private static WorkflowPhaseSkeleton Skeleton(
        bool includeReviewImport = true,
        string workflowId = "phase-impact",
        bool reverseTerminalRoutes = false,
        bool includeOtherPhaseImport = false)
    {
        var root = JsonNode.Parse("""
            {
              "schemaVersion": "loom.workflow-skeleton.v1",
              "workflowId": "phase-impact",
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
                  "imports": ["p00_input.request"], "exports": [],
                  "dependencies": ["p00_input"], "gateIds": [], "terminalRoutes": []
                },
                {
                  "id": "p02_other", "prefix": "p02_other.", "mode": "phaseSliced",
                  "entries": ["p02_other.start"], "exits": ["p02_other.done"],
                  "imports": [], "exports": ["other.notes"],
                  "dependencies": [], "gateIds": [], "terminalRoutes": ["done", "failed"]
                }
              ],
              "routes": [
                {"fromPhaseId":"p00_input","fromExitId":"p00_input.done","toPhaseId":"p01_review","toEntryId":"p01_review.start"}
              ],
              "backedges": []
            }
            """)!.AsObject();
        root["workflowId"] = workflowId;
        if (reverseTerminalRoutes)
        {
            var terminalRoutes = root["phases"]!.AsArray()[2]!["terminalRoutes"]!.AsArray();
            var firstRoute = terminalRoutes[0]!.DeepClone();
            terminalRoutes[0] = terminalRoutes[1]!.DeepClone();
            terminalRoutes[1] = firstRoute;
        }

        if (!includeReviewImport)
        {
            root["phases"]!.AsArray()[1]!["imports"]!.AsArray().Clear();
        }
        if (includeOtherPhaseImport)
        {
            var otherPhase = root["phases"]!.AsArray()[2]!.AsObject();
            otherPhase["imports"]!.AsArray().Add("p00_input.request");
            otherPhase["dependencies"]!.AsArray().Add("p00_input");
        }

        using var document = JsonDocument.Parse(root.ToJsonString());
        return WorkflowPhaseSkeleton.Parse(document.RootElement, "skeleton.json");
    }

    private static WorkflowPhaseSkeleton SkeletonWithoutOther()
    {
        using var document = JsonDocument.Parse("""
            {
              "schemaVersion": "loom.workflow-skeleton.v1",
              "workflowId": "phase-impact",
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
                  "imports": ["p00_input.request"], "exports": [],
                  "dependencies": ["p00_input"], "gateIds": [], "terminalRoutes": []
                }
              ],
              "routes": [
                {"fromPhaseId":"p00_input","fromExitId":"p00_input.done","toPhaseId":"p01_review","toEntryId":"p01_review.start"}
              ],
              "backedges": []
            }
            """);
        return WorkflowPhaseSkeleton.Parse(document.RootElement, "skeleton.json");
    }

    private static WorkflowPhaseSkeleton SkeletonWithTwoRoutes(bool reverseRoutes)
    {
        var root = JsonNode.Parse("""
            {
              "schemaVersion": "loom.workflow-skeleton.v1",
              "workflowId": "phase-impact",
              "phases": [
                {
                  "id": "p00_input", "prefix": "p00_input.", "mode": "phaseSliced",
                  "entries": ["p00_input.start"], "exits": ["p00_input.done", "p00_input.alternate"],
                  "imports": ["p00_input.request"], "exports": ["p00_input.request"],
                  "dependencies": [], "gateIds": [], "terminalRoutes": []
                },
                {
                  "id": "p01_review", "prefix": "p01_review.", "mode": "phaseSliced",
                  "entries": ["p01_review.start", "p01_review.alternate"], "exits": ["p01_review.done"],
                  "imports": ["p00_input.request"], "exports": [],
                  "dependencies": ["p00_input"], "gateIds": [], "terminalRoutes": []
                }
              ],
              "routes": [
                {"fromPhaseId":"p00_input","fromExitId":"p00_input.done","toPhaseId":"p01_review","toEntryId":"p01_review.start"},
                {"fromPhaseId":"p00_input","fromExitId":"p00_input.alternate","toPhaseId":"p01_review","toEntryId":"p01_review.alternate"}
              ],
              "backedges": []
            }
            """)!.AsObject();
        if (reverseRoutes)
        {
            var routes = root["routes"]!.AsArray();
            var first = routes[0]!.DeepClone();
            var second = routes[1]!.DeepClone();
            routes.Clear();
            routes.Add(second);
            routes.Add(first);
        }

        using var document = JsonDocument.Parse(root.ToJsonString());
        return WorkflowPhaseSkeleton.Parse(document.RootElement, "skeleton.json");
    }

    private static ContextFieldCatalog Catalog(string requestType = "string")
    {
        var json = $$"""
            {
              "context_fields": {
                "schemaVersion": "loom.context-fields.v1",
                "fields": [
                  {"path":"p00_input.request","type":"{{requestType}}","nullable":false,"owner":"caller","description":"Input request."},
                  {"path":"other.notes","type":"string","nullable":true,"owner":"agent","description":"Unrelated notes."}
                ]
              }
            }
            """;
        using var document = JsonDocument.Parse(json);
        return ContextFieldCatalog.Parse(document.RootElement, "contract.json");
    }
}
