using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;
using Techne.Loom.SkillOrchestrator.Analysis;
using Techne.Loom.SkillOrchestrator.Runtime;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class WorkflowDesignerContractBehaviorTests
{
    [Fact]
    public void LargeWorkflowDesign_UsesTheFrozenPhaseRouteAcrossBothSkills()
    {
        var root = FindRepositoryRoot();
        var sources = new[]
        {
            File.ReadAllText(Path.Combine(root, ".agents", "skills", "loom-plan-execution", "assets", "agents", "loom-plan-execution-workflow-designer.agent.md")),
            File.ReadAllText(Path.Combine(root, ".agents", "skills", "loom-skill-enhancement", "assets", "agents", "loom-skill-enhancement-workflow-designer.agent.md")),
            File.ReadAllText(Path.Combine(root, ".agents", "skills", "loom-plan-execution", "SKILL.md")),
            File.ReadAllText(Path.Combine(root, ".agents", "skills", "loom-skill-enhancement", "SKILL.md")),
        };
        var requiredTerms = new[]
        {
            "40 or more authored state nodes",
            "loom.workflow-skeleton.v1",
            "skeleton-check",
            "phase-compile",
            "impact",
            "merge",
            "25 authored state nodes",
            "three repair attempts per phase",
            "two post-merge full-compile repair rounds",
            "phase_design_evidence",
        };

        foreach (var source in sources)
        {
            foreach (var term in requiredTerms)
            {
                Assert.Contains(term, source);
            }
        }
    }

    [Fact]
    public void NodeMaps_PrioritizeTheSkillBeingEnhancedMarkdown()
    {
        var root = FindRepositoryRoot();
        var mapPaths = new[]
        {
            Path.Combine(root, ".agents", "skills", "loom-plan-execution", "assets", "so-workflow", "node-to-file-map.md"),
            Path.Combine(root, ".agents", "skills", "loom-skill-enhancement", "assets", "so-workflow", "node-to-file-map.md"),
        };

        foreach (var mapPath in mapPaths)
        {
            var text = File.ReadAllText(mapPath);
            var priorityStart = text.IndexOf("## Priority 1: Skill Being Enhanced", StringComparison.Ordinal);
            var ownershipStart = text.IndexOf("## Document Ownership", StringComparison.Ordinal);
            Assert.True(priorityStart >= 0 && ownershipStart > priorityStart, $"Missing first-priority document mapping in {mapPath}.");
            var prioritySection = text[priorityStart..ownershipStart];
            Assert.Contains("Map the skill being enhanced's `SKILL.md` first", prioritySection);
            Assert.Contains("exact owning node", prioritySection);
            var priorityRowIndex = prioritySection.IndexOf("| **Priority 1:", StringComparison.Ordinal);
            var nextWorkflowRowIndex = prioritySection.IndexOf("| `transition.", StringComparison.Ordinal);
            Assert.True(priorityRowIndex >= 0 && (nextWorkflowRowIndex < 0 || priorityRowIndex < nextWorkflowRowIndex), $"The priority-one document is not the first map row in {mapPath}.");
        }
    }

    [Fact]
    public void DesignerAgents_DeclareTheSameReferenceAndLayeredValidationProtocol()
    {
        var root = FindRepositoryRoot();
        var ao = File.ReadAllText(Path.Combine(root, ".agents", "skills", "loom-plan-execution", "assets", "agents", "loom-plan-execution-workflow-designer.agent.md"));
        var so = File.ReadAllText(Path.Combine(root, ".agents", "skills", "loom-skill-enhancement", "assets", "agents", "loom-skill-enhancement-workflow-designer.agent.md"));
        var sharedTokens = new[]
        {
            "referencePackManifest",
            "schemaDemoInput",
            "generationSetId",
            "previousRunnableReferenceDisposition",
            "reference-manifest.json",
            "static-contract-review.json",
            "semantic-probe-report.json",
            "workflow-designer.reference-manifest.v1",
            "workflow-designer.static-contract-review.v1",
            "workflow-designer.semantic-probe-report.v1",
            "designEvidence",
            "A semantic probe is required whenever the candidate uses",
            "A required probe that is `failed` or `unknown` prevents a `ready` result.",
            "contextFieldTypeComparisons",
            "inspect-contract-fragment",
            "actual producer output",
            "unknown type evidence blocks readiness",
            "before broad tests",
        };

        foreach (var token in sharedTokens)
        {
            Assert.Contains(token, ao);
            Assert.Contains(token, so);
        }

        AssertLayerOrder(Section(ao, "## Failure Triage And Incremental Authoring Gate (Required)", "## Runtime Semantic Evidence Gate (Required)"));
        AssertLayerOrder(Section(so, "## Failure Triage And Incremental Authoring Gate (Required)", "## Runtime Semantic Evidence Gate (Required)"));
        Assert.Contains("## Governance Wrapper Scope Boundary (AO)", ao);
        Assert.Contains("For an ordinary AO business workflow", ao);
        Assert.Contains("## Governance Wrapper Scope Boundary (SO)", so);
        Assert.Contains("full_regeneration", so);
        Assert.Contains("Generation Step 01-15", so);
        Assert.Contains("SO-Specific Design Target", so);
        Assert.Contains("Published Runtime Transport Rule", ao);
        Assert.Contains("strategy: whenAllPass", so);
        Assert.Contains("Legacy wire value `all` is migration-only", so);
    }

    [Fact]
    public void ProducerOutputEvidenceDiscovery_IsReachableFromBothSkillIndexes()
    {
        var root = FindRepositoryRoot();
        var aoRoot = Path.Combine(root, ".agents", "skills", "loom-plan-execution");
        var soRoot = Path.Combine(root, ".agents", "skills", "loom-skill-enhancement");
        var aoEvidencePath = Path.Combine(aoRoot, "reference", "producer-output-evidence-contract.md");
        var soEvidencePath = Path.Combine(soRoot, "reference", "producer-output-evidence-contract.md");
        var aoEvidence = File.ReadAllText(aoEvidencePath);
        var soEvidence = File.ReadAllText(soEvidencePath);

        Assert.Equal(aoEvidence, soEvidence);
        Assert.Contains("Required Discovery Before Asking", aoEvidence);
        Assert.Contains("target-skill driver/agent owns the evidence-sufficiency decision", aoEvidence);
        Assert.Contains("A gate is normally a consumer predicate, not an output producer.", aoEvidence);
        Assert.Contains("Do not assume a universal `ignore` output shape.", aoEvidence);
        Assert.Contains("Ask the user only after completing the discovery above", aoEvidence);

        foreach (var path in new[]
        {
            Path.Combine(aoRoot, "SKILL.md"),
            Path.Combine(aoRoot, "assets", "agents", "loom-plan-execution-workflow-designer.agent.md"),
            Path.Combine(aoRoot, "assets", "so-workflow", "node-to-file-map.md"),
            Path.Combine(soRoot, "SKILL.md"),
            Path.Combine(soRoot, "assets", "agents", "loom-skill-enhancement-workflow-designer.agent.md"),
            Path.Combine(soRoot, "assets", "so-workflow", "node-to-file-map.md"),
        })
        {
            Assert.Contains("producer-output-evidence-contract.md", File.ReadAllText(path));
        }

        using var releaseSet = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "release-set.json")));
        var bundleFiles = releaseSet.RootElement.GetProperty("surfaces").GetProperty("skill_bundle_files")
            .EnumerateArray()
            .Select(item => item.GetString() ?? string.Empty)
            .ToArray();
        Assert.Contains(".agents/skills/loom-plan-execution/reference/producer-output-evidence-contract.md", bundleFiles);
        Assert.Contains(".agents/skills/loom-skill-enhancement/reference/producer-output-evidence-contract.md", bundleFiles);
    }

    [Fact]
    public void PlanExecutionTemplate_UsesCanonicalStructuredTransitionFields()
    {
        var root = FindRepositoryRoot();
        var templatePath = Path.Combine(root, ".agents", "skills", "loom-plan-execution", "assets", "so-workflow", "so-template.json");
        using var document = JsonDocument.Parse(File.ReadAllText(templatePath));
        var transitions = document.RootElement.GetProperty("nodes")
            .EnumerateObject()
            .Where(node => node.Value.GetProperty("$kind").GetString() != "state")
            .ToArray();

        Assert.NotEmpty(transitions);
        foreach (var entry in transitions)
        {
            Assert.False(entry.Value.TryGetProperty("succeedExpression", out _), $"{entry.Name} uses legacy succeedExpression.");
            Assert.False(entry.Value.TryGetProperty("maxRetry", out _), $"{entry.Name} uses legacy maxRetry.");

            foreach (var fieldName in new[] { "guardExpression", "passExpression" })
            {
                var expression = entry.Value.GetProperty(fieldName);
                Assert.Equal(JsonValueKind.Object, expression.ValueKind);
                Assert.Equal("predicate", expression.GetProperty("kind").GetString());
                Assert.False(string.IsNullOrWhiteSpace(expression.GetProperty("source").GetString()));
                Assert.Equal("Evaluate", expression.GetProperty("entryPoint").GetString());
                Assert.Equal("bool", expression.GetProperty("resultType").GetString());
            }
        }
        var workflow = WorkflowJsonSerializer.Deserialize(File.ReadAllText(templatePath));
        var refresh = Assert.IsType<CommandTransition>(workflow.Nodes["transition.refresh_governed_template"]);
        Assert.Contains("phase_design_evidence.verdict", refresh.SucceedExpression.Source);
        Assert.Contains("phase_design_evidence.record_key", refresh.SucceedExpression.Source);
        Assert.Contains("phase_design_evidence.record_path", refresh.SucceedExpression.Source);
        var refreshParameters = Assert.IsAssignableFrom<IDictionary<string, object?>>(refresh.Command.Parameters);
        var priorityDocumentMap = Assert.IsAssignableFrom<IDictionary<string, object?>>(refreshParameters["priorityDocumentMap"]);
        Assert.Equal("1", priorityDocumentMap["priority"]);
        Assert.Equal("SKILL.md", priorityDocumentMap["document"]);
        var contextFieldTypePolicy = Assert.IsAssignableFrom<IDictionary<string, object?>>(refreshParameters["contextFieldTypePolicy"]);
        Assert.Equal("contextFieldTypeComparisons", contextFieldTypePolicy["evidenceKey"]);
        Assert.Equal("block", contextFieldTypePolicy["unknownAction"]);
        Assert.Equal("true", contextFieldTypePolicy["probeBeforeBroadTests"]);
        Assert.Equal("reference/producer-output-evidence-contract.md", contextFieldTypePolicy["evidenceDiscoveryReference"]);
        Assert.Equal("true", contextFieldTypePolicy["inspectTargetSkillFirst"]);
        Assert.Equal("only-after-required-discovery", contextFieldTypePolicy["userQuestionTiming"]);
        Assert.Equal("workflow-envelope-only", contextFieldTypePolicy["schemaDemoScope"]);
        Assert.Equal("consumer-predicate-not-producer", contextFieldTypePolicy["gateRole"]);
    }

    [Fact]
    public void SoDraftTemplate_EvidencePredicateFailsClosedForWrongVersionOrUnknownProbe()
    {
        var root = FindRepositoryRoot();
        var templatePath = Path.Combine(root, ".agents", "skills", "loom-skill-enhancement", "assets", "so-workflow", "so-template.json");
        var workflow = WorkflowJsonSerializer.Deserialize(File.ReadAllText(templatePath));
        var draft = Assert.IsType<CommandTransition>(workflow.Nodes["transition.draft_template"]);
        var evaluator = new CSharpExpressionEvaluator(workflow.ExpressionBinding);
        var context = CreateEvidenceContext("workflow-designer.reference-manifest.v1", "passed");

        Assert.True(evaluator.EvaluateBoolean(draft.SucceedExpression.Source, context));

        var developmentContext = CreateEvidenceContext("workflow-designer.reference-manifest.v1", "passed");
        foreach (var (descriptorName, hashName) in new[]
        {
            (DescriptorName: "reference_manifest", HashName: "sha256"),
            (DescriptorName: "static_contract_review", HashName: "sha256"),
            (DescriptorName: "semantic_probe_report", HashName: "sha256"),
            (DescriptorName: "schema_demo_input", HashName: "schemaSha256"),
            (DescriptorName: "schema_demo_input", HashName: "demoSha256"),
        })
        {
            var descriptor = Assert.IsAssignableFrom<IDictionary<string, object?>>(developmentContext[descriptorName]);
            descriptor.Remove(hashName);
        }
        Assert.True(evaluator.EvaluateBoolean(draft.SucceedExpression.Source, developmentContext));

        var wrongSchemaContext = CreateEvidenceContext("wrong-version", "passed");
        Assert.False(evaluator.EvaluateBoolean(draft.SucceedExpression.Source, wrongSchemaContext));

        var unknownProbeContext = CreateEvidenceContext("workflow-designer.reference-manifest.v1", "unknown");
        Assert.False(evaluator.EvaluateBoolean(draft.SucceedExpression.Source, unknownProbeContext));

        var missingPhaseEvidence = CreateEvidenceContext("workflow-designer.reference-manifest.v1", "passed");
        missingPhaseEvidence.Remove("phase_design_evidence");
        Assert.False(evaluator.EvaluateBoolean(draft.SucceedExpression.Source, missingPhaseEvidence));

        var failedPhaseEvidence = CreateEvidenceContext("workflow-designer.reference-manifest.v1", "passed");
        var failedPhase = Assert.IsAssignableFrom<IDictionary<string, object?>>(failedPhaseEvidence["phase_design_evidence"]);
        failedPhase["verdict"] = "failed";
        Assert.False(evaluator.EvaluateBoolean(draft.SucceedExpression.Source, failedPhaseEvidence));

        var unlinkedPhaseEvidence = CreateEvidenceContext("workflow-designer.reference-manifest.v1", "passed");
        var unlinkedPhase = Assert.IsAssignableFrom<IDictionary<string, object?>>(unlinkedPhaseEvidence["phase_design_evidence"]);
        unlinkedPhase["record_key"] = "anotherRecord";
        Assert.False(evaluator.EvaluateBoolean(draft.SucceedExpression.Source, unlinkedPhaseEvidence));

        var mismatchedPhasePath = CreateEvidenceContext("workflow-designer.reference-manifest.v1", "passed");
        var mismatchedPhase = Assert.IsAssignableFrom<IDictionary<string, object?>>(mismatchedPhasePath["phase_design_evidence"]);
        mismatchedPhase["record_path"] = "C:/temp/workflow-design/other.json";
        Assert.False(evaluator.EvaluateBoolean(draft.SucceedExpression.Source, mismatchedPhasePath));
    }

    [Fact]
    public void SharedReviewContextGate_DoesNotRequireSupportHash()
    {
        var root = FindRepositoryRoot();
        var templatePath = Path.Combine(root, ".agents", "skills", "loom-skill-enhancement", "assets", "so-workflow", "so-template.json");
        var templateJson = File.ReadAllText(templatePath);
        using var document = JsonDocument.Parse(templateJson);
        var passExpression = document.RootElement.GetProperty("validation")
            .GetProperty("gates")
            .GetProperty("gate.bootstrap_shared_context")
            .GetProperty("passExpression")
            .GetString();
        var workflow = WorkflowJsonSerializer.Deserialize(templateJson);
        var evaluator = new CSharpExpressionEvaluator(workflow.ExpressionBinding);
        var context = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["shared_review_context"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["bounded"] = true,
                ["source_manifest"] = Array.Empty<object>(),
            },
        };

        Assert.True(evaluator.EvaluateBoolean(passExpression!, context));
    }

    private static Dictionary<string, object?> CreateEvidenceContext(string referenceSchemaVersion, string probeVerdict)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["workflow_template_json"] = "assets/so-workflow/so-template.json",
            ["workflow_designer_dispatch_record"] = "workflow-designer dispatched with relative-link context",
            ["gate_failure_guidance_review"] = new Dictionary<string, object?>(),
            ["workflow_design_evidence"] = new Dictionary<string, object?>(),
            ["phase_design_evidence"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["mode"] = "phaseSliced",
                ["authored_state_node_count"] = 50,
                ["verdict"] = "passed",
                ["record_key"] = "phaseDesignEvidence",
                ["record_path"] = "C:/temp/workflow-design/static-contract-review.json",
            },
            ["layered_static_validation"] = new Dictionary<string, object?>(),
            ["expression_audit"] = new Dictionary<string, object?>(),
            ["projection_matrix"] = new Dictionary<string, object?>(),
            ["gate_producer_route_matrix"] = new Dictionary<string, object?>(),
            ["reference_manifest"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["schemaVersion"] = referenceSchemaVersion,
                ["verdict"] = "passed",
                ["path"] = "C:/temp/workflow-design/reference-manifest.json",
                ["sha256"] = new string('a', 64),
                ["runtimeVersion"] = "0.3.258-beta",
                ["generationSetId"] = "fixture-generation-set",
            },
            ["static_contract_review"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["schemaVersion"] = "workflow-designer.static-contract-review.v1",
                ["verdict"] = "passed",
                ["path"] = "C:/temp/workflow-design/static-contract-review.json",
                ["sha256"] = new string('b', 64),
                ["runtimeVersion"] = "0.3.258-beta",
            },
            ["schema_demo_input"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["runtime"] = "so",
                ["runtimeBinding"] = "dotnet-so",
                ["runtimeVersion"] = "0.3.258-beta",
                ["generationSetId"] = "fixture-generation-set",
                ["schemaFile"] = "C:/temp/workflow.schema.json",
                ["demoFile"] = "C:/temp/workflow.demo.json",
                ["demoCompileAudit"] = "C:/temp/workflow.demo.compile.audit.json",
                ["schemaSha256"] = new string('d', 64),
                ["demoSha256"] = new string('e', 64),
            },
            ["semantic_probe_report"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["schemaVersion"] = "workflow-designer.semantic-probe-report.v1",
                ["verdict"] = probeVerdict,
                ["path"] = "C:/temp/workflow-design/semantic-probe-report.json",
                ["sha256"] = new string('c', 64),
                ["runtimeVersion"] = "0.3.258-beta",
            },
        };
    }

    [Fact]

    public void Contracts_DoNotContainDuplicateJsonKeys()

    {

        var root = FindRepositoryRoot();

        foreach (var relativePath in new[]

        {

            Path.Combine(".agents", "skills", "loom-plan-execution", "contract.json"),

            Path.Combine(".agents", "skills", "loom-skill-enhancement", "assets", "so-workflow", "contract.json"),

            Path.Combine(".agents", "skills", "loom-skill-enhancement", "assets", "so-workflow", "so-template.json")

        })

        {

            var path = Path.Combine(root, relativePath);

            using var document = JsonDocument.Parse(File.ReadAllText(path));

            AssertNoDuplicateKeys(document.RootElement, path, "$" );

        }

    }



    [Fact]
    public void Contracts_DeclareReferencePackAndEvidenceOutputs()
    {
        var root = FindRepositoryRoot();
        var contractPaths = new[]
        {
            Path.Combine(root, ".agents", "skills", "loom-plan-execution", "contract.json"),
            Path.Combine(root, ".agents", "skills", "loom-skill-enhancement", "assets", "so-workflow", "contract.json")
        };
        var requiredInputs = new[]
        {
            "reference_pack_manifest",
            "schema_demo_input",
            "previous_runnable_reference",
            "workflow_design_output_root",
            "gate_failure_guidance_contract"
        };
        var requiredOutputs = new[]
        {
            "reference_manifest",
            "reference_authority_decision",
            "layered_static_validation",
            "expression_audit",
            "projection_matrix",
            "gate_producer_route_matrix",
            "previous_runnable_reference_disposition",
            "static_contract_review",
            "semantic_probe_report",
            "workflow_design_evidence",
            "phase_design_evidence",
            "workflow_designer_dispatch_record",
            "gate_failure_guidance_review"
        };

        foreach (var path in contractPaths)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var rootElement = document.RootElement;
            var inputs = rootElement.GetProperty("inputs");
            var outputs = rootElement.GetProperty("outputs");
            foreach (var name in requiredInputs)
            {
                Assert.True(inputs.TryGetProperty(name, out _), $"Missing input '{name}' in {path}");
            }
            foreach (var name in requiredOutputs)
            {
                Assert.True(outputs.TryGetProperty(name, out _), $"Missing output '{name}' in {path}");
            }

            Assert.Contains("workflow-designer.reference-manifest.v1", outputs.GetProperty("reference_manifest").GetString());
            Assert.Contains("workflow-designer.static-contract-review.v1", outputs.GetProperty("static_contract_review").GetString());
            Assert.Contains("workflow-designer.semantic-probe-report.v1", outputs.GetProperty("semantic_probe_report").GetString());
            Assert.Contains("runtime-owned", outputs.GetProperty("workflow_design_evidence").GetString());
            var phaseDescription = outputs.GetProperty("phase_design_evidence").GetString();
            Assert.Contains("phaseDesignEvidence", phaseDescription);
            Assert.Contains("static-contract-review.json", phaseDescription);
            Assert.Contains("no fourth report", phaseDescription);
            var staticReviewDescription = outputs.GetProperty("static_contract_review").GetString() ?? string.Empty;
            Assert.Contains("contextFieldTypeComparisons", staticReviewDescription);
            Assert.Contains("actual producer output", staticReviewDescription);
            Assert.Contains("unknown", staticReviewDescription);
            var contextFields = rootElement.GetProperty("context_fields").GetProperty("fields").EnumerateArray();
            Assert.Contains(contextFields, field => field.GetProperty("path").GetString() == "phase_design_evidence.verdict");
            Assert.Contains(contextFields, field => field.GetProperty("path").GetString() == "phase_design_evidence.record_path");
        }
    }

    [Fact]
    public void SoDraftTemplate_ExposesEvidenceDescriptorsAndFailClosedPredicates()
    {
        var root = FindRepositoryRoot();
        var templatePath = Path.Combine(root, ".agents", "skills", "loom-skill-enhancement", "assets", "so-workflow", "so-template.json");
        var workflow = WorkflowJsonSerializer.Deserialize(File.ReadAllText(templatePath));
        var draft = Assert.IsType<CommandTransition>(workflow.Nodes["transition.draft_template"]);
        var parameters = Assert.IsAssignableFrom<IDictionary<string, object?>>(draft.Command.Parameters);

        foreach (var input in new[] { "reference_pack_manifest", "schema_demo_input", "workflow_design_output_root" })
        {
            Assert.Contains(input, GetStringList(parameters["requiredInputs"]));
        }

        foreach (var family in new[]
        {
            "workflow_design_evidence",
            "phase_design_evidence",
            "reference_manifest",
            "static_contract_review",
            "semantic_probe_report",
            "reference_authority_decision",
            "layered_static_validation",
            "expression_audit",
            "projection_matrix",
            "gate_producer_route_matrix",
            "previous_runnable_reference_disposition"
        })
        {
            Assert.Contains(family, draft.PublishesOutputFamilies ?? []);
            Assert.Contains(family, GetStringList(parameters["requiredInputs"]));
            Assert.True(GetBindings(parameters).ContainsKey(family), $"Missing output binding for {family}");
        }

        var designerInputs = Assert.IsAssignableFrom<IDictionary<string, object?>>(parameters["designerInputs"]);
        Assert.Contains("referencePackManifest", designerInputs.Keys);
        Assert.Contains("schemaDemoInput", designerInputs.Keys);
        Assert.Contains("workflowDesignOutputRoot", designerInputs.Keys);
        Assert.Equal("$context:aggregated_plan_findings.plan.evidence_review", designerInputs["evidenceNodeMapAnalysis"]);
        var priorityDocumentMap = Assert.IsAssignableFrom<IDictionary<string, object?>>(designerInputs["priorityDocumentMap"]);
        Assert.Equal("1", priorityDocumentMap["priority"]);
        Assert.Equal("SKILL.md", priorityDocumentMap["document"]);
        var contextFieldTypePolicy = Assert.IsAssignableFrom<IDictionary<string, object?>>(designerInputs["contextFieldTypePolicy"]);
        Assert.Equal("contextFieldTypeComparisons", contextFieldTypePolicy["evidenceKey"]);
        Assert.Equal("parse-confirmation-only", contextFieldTypePolicy["catalogEcho"]);
        Assert.Equal("block", contextFieldTypePolicy["unknownAction"]);
        Assert.Equal("true", contextFieldTypePolicy["probeBeforeBroadTests"]);
        Assert.Equal("reference/producer-output-evidence-contract.md", contextFieldTypePolicy["evidenceDiscoveryReference"]);
        Assert.Equal("true", contextFieldTypePolicy["inspectTargetSkillFirst"]);
        Assert.Equal("only-after-required-discovery", contextFieldTypePolicy["userQuestionTiming"]);
        Assert.Equal("workflow-envelope-only", contextFieldTypePolicy["schemaDemoScope"]);
        Assert.Equal("consumer-predicate-not-producer", contextFieldTypePolicy["gateRole"]);
        var previousEvidence = Assert.IsAssignableFrom<IDictionary<string, object?>>(designerInputs["previousDesignEvidence"]);
        Assert.Contains("referenceManifest", previousEvidence.Keys);
        Assert.Contains("staticContractReview", previousEvidence.Keys);
        Assert.Contains("semanticProbeReport", previousEvidence.Keys);

        var source = draft.SucceedExpression.Source;
        Assert.Contains("workflow-designer.reference-manifest.v1", source);
        Assert.Contains("workflow-designer.static-contract-review.v1", source);
        Assert.Contains("workflow-designer.semantic-probe-report.v1", source);
        Assert.Contains("reference_manifest.verdict", source);
        Assert.Contains("static_contract_review.verdict", source);
        Assert.Contains("semantic_probe_report.verdict", source);
        Assert.Contains("reference_manifest.path", source);
        Assert.Contains("static_contract_review.path", source);
        Assert.Contains("semantic_probe_report.runtimeVersion", source);
        Assert.Contains("schema_demo_input.runtimeBinding", source);
        Assert.Contains("schema_demo_input.generationSetId", source);
        Assert.DoesNotContain("reference_manifest.sha256", source);
        Assert.DoesNotContain("static_contract_review.sha256", source);
        Assert.DoesNotContain("semantic_probe_report.sha256", source);
        Assert.DoesNotContain("schema_demo_input.schemaSha256", source);
        Assert.DoesNotContain("schema_demo_input.demoSha256", source);
    }

    [Fact]
    public void SoDraftTemplate_DataflowSeparatesPayloadRequirementsFromProducedContext()
    {
        var root = FindRepositoryRoot();
        var templatePath = Path.Combine(root, ".agents", "skills", "loom-skill-enhancement", "assets", "so-workflow", "so-template.json");
        var workflow = WorkflowJsonSerializer.Deserialize(File.ReadAllText(templatePath));
        var report = new SkillWorkflowDataflowAnalyzer().Analyze(workflow);
        var transition = Assert.Single(report.Transitions, item => item.TransitionId == "transition.draft_template");

        Assert.Equal("workflow_template_json", transition.ResumeOutputKey);
        Assert.Equal("assets/so-workflow/so-template.json", transition.OutputPath);
        Assert.Contains("workflow_design_evidence", transition.InputPaths);
        Assert.Contains("phase_design_evidence", transition.InputPaths);
        Assert.Contains("workflow_design_evidence", transition.PayloadPaths);
        Assert.Contains("workflow_design_evidence", transition.ProducedContextPaths);
        Assert.Contains("workflow_design_evidence", transition.PublishedOutputFamilies);
        Assert.Empty(transition.UnresolvedOutputFamilies);
    }

    private static void AssertNoDuplicateKeys(JsonElement element, string filePath, string objectPath)

    {

        if (element.ValueKind == JsonValueKind.Object)

        {

            var names = new HashSet<string>(StringComparer.Ordinal);

            foreach (var property in element.EnumerateObject())

            {

                Assert.True(names.Add(property.Name), $"Duplicate JSON key '{property.Name}' at {objectPath} in {filePath}");

                AssertNoDuplicateKeys(property.Value, filePath, objectPath + "/" + property.Name);

            }

        }

        else if (element.ValueKind == JsonValueKind.Array)

        {

            var index = 0;

            foreach (var item in element.EnumerateArray())

            {

                AssertNoDuplicateKeys(item, filePath, objectPath + "/" + index);

                index++;

            }

        }

    }



    private static IDictionary<string, object?> GetBindings(IDictionary<string, object?> parameters)
    {
        return Assert.IsAssignableFrom<IDictionary<string, object?>>(parameters["outputBindings"]);
    }

    private static IReadOnlyList<string> GetStringList(object? value)
    {
        return Assert.IsAssignableFrom<IEnumerable<object?>>(value).Select(Convert.ToString).Where(item => !string.IsNullOrWhiteSpace(item)).Cast<string>().ToArray();
    }

    private static void AssertLayerOrder(string section)
    {
        var layers = new[]
        {
            "**Runtime/preflight**",
            "**JSON**",
            "**Graph**",
            "**Enum**",
            "**Expression**",
            "**Projection**",
            "**Dataflow**",
            "**Gate**",
            "**Ownership**",
            "**Semantic**"
        };
        var previous = -1;
        foreach (var layer in layers)
        {
            var current = section.IndexOf(layer, previous + 1, StringComparison.Ordinal);
            Assert.True(current >= 0, $"Missing layer '{layer}'");
            Assert.True(current > previous, $"Layer '{layer}' is out of order");
            previous = current;
        }
    }

    private static string Section(string content, string startMarker, string endMarker)
    {
        var start = content.IndexOf(startMarker, StringComparison.Ordinal);
        var end = content.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, $"Could not isolate section {startMarker}");
        return content.Substring(start, end - start);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Techne.Loom.sln")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate Techne-Loom repository root.");
    }
}
