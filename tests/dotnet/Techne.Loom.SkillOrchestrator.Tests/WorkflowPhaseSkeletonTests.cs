using System.Text.Json;
using System.Text.Json.Nodes;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class WorkflowPhaseSkeletonTests
{
    [Fact]
    public void ParseAndValidateDerivesTypedPortsFromOwningCatalog()
    {
        var skeleton = ParseSkeleton(SkeletonJson());
        var result = WorkflowPhaseSkeletonValidator.Validate(skeleton, Catalog(), "skeleton.json");

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Diagnostics.Select(static diagnostic => diagnostic.Message)));
        var dynamicPort = Assert.Single(result.Ports, static port =>
            port.PhaseId == "p01_review" && port.Direction == "import" && port.Path == "p00_input.options.answer");
        Assert.Equal("string", dynamicPort.Type);
        Assert.False(dynamicPort.Nullable);
        Assert.Equal("caller", dynamicPort.Owner);
        var decisionPort = Assert.Single(result.Ports, static port =>
            port.PhaseId == "p01_review" && port.Direction == "export" && port.Path == "review.approved");
        Assert.Equal("boolean", decisionPort.Type);
        Assert.Equal("agent", decisionPort.Owner);
    }

    [Fact]
    public void ImportRequiresDirectProducerDependency()
    {
        var skeleton = ParseSkeleton(SkeletonJson(declareInputDependency: false));
        var result = WorkflowPhaseSkeletonValidator.Validate(skeleton, Catalog(), "skeleton.json");

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == "LOOM.PHASE.IMPORT_DEPENDENCY_MISSING");
    }

    [Fact]
    public void AgentOwnedImportRequiresAProducer()
    {
        var root = JsonNode.Parse(SkeletonJson())!.AsObject();
        root["phases"]!.AsArray()[1]!["exports"]!.AsArray().Clear();
        root["phases"]!.AsArray()[1]!["imports"]!.AsArray().Add("review.approved");
        using var document = JsonDocument.Parse(root.ToJsonString());
        var skeleton = WorkflowPhaseSkeleton.Parse(document.RootElement, "skeleton.json");
        var result = WorkflowPhaseSkeletonValidator.Validate(skeleton, Catalog(), "skeleton.json");

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == "LOOM.PHASE.FIELD_PRODUCER_MISSING");
    }

    [Fact]
    public void UnknownPortFailsCatalogValidation()
    {
        var root = JsonNode.Parse(SkeletonJson())!.AsObject();
        root["phases"]!.AsArray()[1]!["exports"]![0] = "review.unlisted";
        using var document = JsonDocument.Parse(root.ToJsonString());
        var skeleton = WorkflowPhaseSkeleton.Parse(document.RootElement, "skeleton.json");
        var result = WorkflowPhaseSkeletonValidator.Validate(skeleton, Catalog(), "skeleton.json");

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == "LOOM.PHASE.PORT_UNKNOWN");
    }

    [Fact]
    public void SharedFieldCannotHaveMultipleProducingPhases()
    {
        var root = JsonNode.Parse(SkeletonJson())!.AsObject();
        root["phases"]!.AsArray().Add(JsonNode.Parse("""
            {
              "id": "legacy_review",
              "prefix": "",
              "mode": "legacy",
              "entries": ["old.start"],
              "exits": ["old.done"],
              "imports": [],
              "exports": ["review.approved"],
              "dependencies": [],
              "gateIds": [],
              "terminalRoutes": []
            }
            """));
        using var document = JsonDocument.Parse(root.ToJsonString());
        var skeleton = WorkflowPhaseSkeleton.Parse(document.RootElement, "skeleton.json");
        var result = WorkflowPhaseSkeletonValidator.Validate(skeleton, Catalog(), "skeleton.json");

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == "LOOM.PHASE.MULTIPLE_PRODUCERS");
    }

    [Fact]
    public void LegacyPhaseIsWarningButStillUsesTypedCatalogChecks()
    {
        var root = JsonNode.Parse(SkeletonJson())!.AsObject();
        foreach (var phase in root["phases"]!.AsArray())
        {
            phase!["mode"] = "legacy";
            phase["prefix"] = "";
        }

        using var document = JsonDocument.Parse(root.ToJsonString());
        var skeleton = WorkflowPhaseSkeleton.Parse(document.RootElement, "skeleton.json");
        var result = WorkflowPhaseSkeletonValidator.Validate(skeleton, Catalog(), "skeleton.json");

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Diagnostics.Select(static diagnostic => diagnostic.Message)));
        Assert.Equal(2, result.Diagnostics.Count(static diagnostic =>
            diagnostic.Severity == WorkflowPhaseDiagnosticSeverity.Warning
            && diagnostic.Code == "LOOM.PHASE.LEGACY_PHASE"));
    }

    [Fact]
    public void MixedLegacyAndPhaseSlicedMigrationRetainsTypedInterfacesAndWarnings()
    {
        var root = JsonNode.Parse(SkeletonJson())!.AsObject();
        var phases = root["phases"]!.AsArray();
        var legacyPhase = phases[0]!.AsObject();
        legacyPhase["id"] = "legacy_intake";
        legacyPhase["prefix"] = "";
        legacyPhase["mode"] = "legacy";
        legacyPhase["entries"] = new JsonArray(JsonValue.Create("intake.start"));
        legacyPhase["exits"] = new JsonArray(JsonValue.Create("intake.done"));
        phases[1]!["dependencies"]![0] = "legacy_intake";
        var route = root["routes"]!.AsArray()[0]!.AsObject();
        route["fromPhaseId"] = "legacy_intake";
        route["fromExitId"] = "intake.done";

        using var document = JsonDocument.Parse(root.ToJsonString());
        var skeleton = WorkflowPhaseSkeleton.Parse(document.RootElement, "skeleton.json");
        var result = WorkflowPhaseSkeletonValidator.Validate(skeleton, Catalog(), "skeleton.json");

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Diagnostics.Select(static diagnostic => diagnostic.Message)));
        Assert.Equal("legacy", skeleton.Phases[0].Mode);
        Assert.Equal("phaseSliced", skeleton.Phases[1].Mode);
        var warning = Assert.Single(result.Diagnostics, static diagnostic =>
            diagnostic.Code == "LOOM.PHASE.LEGACY_PHASE");
        Assert.Equal("legacy_intake", warning.PhaseId);
        var legacyExport = Assert.Single(result.Ports, static port =>
            port.PhaseId == "legacy_intake" && port.Direction == "export" && port.Path == "p00_input.request");
        Assert.Equal("string", legacyExport.Type);
        Assert.Equal("caller", legacyExport.Owner);

        var invalidRoot = root.DeepClone().AsObject();
        invalidRoot["phases"]!.AsArray()[0]!["exports"]![0] = "p00_input.unlisted";
        using var invalidDocument = JsonDocument.Parse(invalidRoot.ToJsonString());
        var invalidSkeleton = WorkflowPhaseSkeleton.Parse(invalidDocument.RootElement, "mixed-invalid.json");
        var invalidResult = WorkflowPhaseSkeletonValidator.Validate(invalidSkeleton, Catalog(), "mixed-invalid.json");

        Assert.False(invalidResult.IsValid);
        Assert.Contains(invalidResult.Diagnostics, static diagnostic =>
            diagnostic.Code == "LOOM.PHASE.PORT_UNKNOWN" && diagnostic.PhaseId == "legacy_intake");
    }

    [Fact]
    public void CallerOwnedPathsRequireReservedInputPrefix()
    {
        using var contract = JsonDocument.Parse("""
            {
              "context_fields": {
                "schemaVersion": "loom.context-fields.v1",
                "fields": [
                  {"path":"request.text","type":"string","nullable":false,"owner":"caller","description":"Request text."}
                ]
              }
            }
            """);
        var catalog = ContextFieldCatalog.Parse(contract.RootElement, "contract.json");
        var root = JsonNode.Parse(SkeletonJson())!.AsObject();
        root["phases"]!.AsArray()[0]!["imports"]![0] = "request.text";
        root["phases"]!.AsArray()[0]!["exports"]![0] = "request.text";
        using var document = JsonDocument.Parse(root.ToJsonString());
        var skeleton = WorkflowPhaseSkeleton.Parse(document.RootElement, "skeleton.json");
        var result = WorkflowPhaseSkeletonValidator.Validate(skeleton, catalog, "skeleton.json");

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == "LOOM.PHASE.CALLER_INPUT_PREFIX");
    }

    [Fact]
    public void ParseRejectsMultipleTargetsForOneExit()
    {
        var root = JsonNode.Parse(SkeletonJson())!.AsObject();
        root["phases"]!.AsArray()[1]!["entries"]!.AsArray().Add("p01_review.alternate");
        root["routes"]!.AsArray().Add(JsonNode.Parse("""
            {"fromPhaseId":"p00_input","fromExitId":"p00_input.done","toPhaseId":"p01_review","toEntryId":"p01_review.alternate"}
            """));
        using var document = JsonDocument.Parse(root.ToJsonString());

        var error = Assert.Throws<InvalidOperationException>(() =>
            WorkflowPhaseSkeleton.Parse(document.RootElement, "skeleton.json"));

        Assert.Contains("has more than one cross-phase target", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseRejectsUnsupportedSchemaVersion()
    {
        using var document = JsonDocument.Parse(SkeletonJson().Replace(
            WorkflowPhaseSkeleton.CurrentSchemaVersion,
            "loom.workflow-skeleton.v9",
            StringComparison.Ordinal));

        var error = Assert.Throws<InvalidOperationException>(() => WorkflowPhaseSkeleton.Parse(document.RootElement, "skeleton.json"));

        Assert.Contains(WorkflowPhaseSkeleton.CurrentSchemaVersion, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ParentExportCoversDeclaredChildImport()
    {
        var root = JsonNode.Parse(SkeletonJson())!.AsObject();
        root["phases"]!.AsArray()[0]!["exports"]!.AsArray().Clear();
        root["phases"]!.AsArray()[0]!["exports"]!.AsArray().Add("p00_input.options");
        using var document = JsonDocument.Parse(root.ToJsonString());
        var skeleton = WorkflowPhaseSkeleton.Parse(document.RootElement, "skeleton.json");
        var result = WorkflowPhaseSkeletonValidator.Validate(skeleton, Catalog(), "skeleton.json");

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Diagnostics.Select(static diagnostic => diagnostic.Message)));
    }

    [Fact]
    public void ChildExportCoversDeclaredParentImport()
    {
        var root = JsonNode.Parse(SkeletonJson())!.AsObject();
        root["phases"]!.AsArray()[1]!["imports"]!.AsArray().Add("p00_input.options");
        using var document = JsonDocument.Parse(root.ToJsonString());
        var skeleton = WorkflowPhaseSkeleton.Parse(document.RootElement, "skeleton.json");
        var result = WorkflowPhaseSkeletonValidator.Validate(skeleton, Catalog(), "skeleton.json");

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Diagnostics.Select(static diagnostic => diagnostic.Message)));
    }

    [Fact]
    public void ParentAndChildExportsCannotHaveDifferentProducers()
    {
        var root = JsonNode.Parse(SkeletonJson())!.AsObject();
        root["phases"]!.AsArray().Add(JsonNode.Parse("""
            {
              "id": "legacy_options",
              "prefix": "",
              "mode": "legacy",
              "entries": ["old.start"],
              "exits": ["old.done"],
              "imports": [],
              "exports": ["p00_input.options"],
              "dependencies": [],
              "gateIds": [],
              "terminalRoutes": []
            }
            """));
        using var document = JsonDocument.Parse(root.ToJsonString());
        var skeleton = WorkflowPhaseSkeleton.Parse(document.RootElement, "skeleton.json");
        var result = WorkflowPhaseSkeletonValidator.Validate(skeleton, Catalog(), "skeleton.json");

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == "LOOM.PHASE.PARENT_CHILD_PRODUCER_CONFLICT");
    }

    private static WorkflowPhaseSkeleton ParseSkeleton(string json)
    {
        using var document = JsonDocument.Parse(json);
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
                  {"path":"p00_input.options","type":"object","valueType":"string","nullable":true,"owner":"caller","description":"Input options."},
                  {"path":"review.approved","type":"boolean","nullable":false,"owner":"agent","description":"Approval decision."}
                ]
              }
            }
            """);
        return ContextFieldCatalog.Parse(document.RootElement, "contract.json");
    }

    private static string SkeletonJson(bool declareInputDependency = true)
    {
        var dependencies = JsonSerializer.Serialize(declareInputDependency ? new[] { "p00_input" } : Array.Empty<string>());
        return $$"""
            {
              "schemaVersion": "loom.workflow-skeleton.v1",
              "workflowId": "intake-review",
              "phases": [
                {
                  "id": "p00_input",
                  "prefix": "p00_input.",
                  "mode": "phaseSliced",
                  "entries": ["p00_input.start"],
                  "exits": ["p00_input.done"],
                  "imports": ["p00_input.request", "p00_input.options.answer"],
                  "exports": ["p00_input.request", "p00_input.options.answer"],
                  "dependencies": [],
                  "gateIds": [],
                  "terminalRoutes": []
                },
                {
                  "id": "p01_review",
                  "prefix": "p01_review.",
                  "mode": "phaseSliced",
                  "entries": ["p01_review.start"],
                  "exits": ["p01_review.done"],
                  "imports": ["p00_input.request", "p00_input.options.answer"],
                  "exports": ["review.approved"],
                  "dependencies": {{dependencies}},
                  "gateIds": ["review_approved"],
                  "terminalRoutes": ["done"]
                }
              ],
              "routes": [
                {
                  "fromPhaseId": "p00_input",
                  "fromExitId": "p00_input.done",
                  "toPhaseId": "p01_review",
                  "toEntryId": "p01_review.start"
                }
              ],
              "backedges": []
            }
            """;
    }
}
