using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
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
            ReadDesignerContract(Path.Combine(root, ".agents", "skills", "loom-plan-execution", "assets", "agents", "loom-plan-execution-workflow-designer.agent.md")),
            ReadDesignerContract(Path.Combine(root, ".agents", "skills", "loom-skill-enhancement", "assets", "agents", "loom-skill-enhancement-workflow-designer.agent.md")),
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
        var ao = ReadDesignerContract(Path.Combine(root, ".agents", "skills", "loom-plan-execution", "assets", "agents", "loom-plan-execution-workflow-designer.agent.md"));
        var so = ReadDesignerContract(Path.Combine(root, ".agents", "skills", "loom-skill-enhancement", "assets", "agents", "loom-skill-enhancement-workflow-designer.agent.md"));
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
        var aoSkill = File.ReadAllText(Path.Combine(aoRoot, "SKILL.md"));
        var soSkill = File.ReadAllText(Path.Combine(soRoot, "SKILL.md"));
        var aoDesigner = ReadDesignerContract(Path.Combine(aoRoot, "assets", "agents", "loom-plan-execution-workflow-designer.agent.md"));
        var soDesigner = ReadDesignerContract(Path.Combine(soRoot, "assets", "agents", "loom-skill-enhancement-workflow-designer.agent.md"));
        var markdownReviewer = File.ReadAllText(Path.Combine(soRoot, "assets", "agents", "loom-skill-enhancement-skill-markdown-gap-review.agent.md"));
        var findingsAggregator = File.ReadAllText(Path.Combine(soRoot, "assets", "agents", "loom-skill-enhancement-review-findings-aggregator.agent.md"));

        Assert.Contains("Runtime Documentation References", aoSkill);
        Assert.Contains("Every target skill's `SKILL.md` must carry the conditional `Runtime Documentation References` rule", aoSkill);
        Assert.Contains("Require every target `SKILL.md` to carry the conditional `Runtime Documentation References` rule", soSkill);
        Assert.Contains("docs_root", aoSkill);
        Assert.Contains("referencePackManifest", aoSkill);
        Assert.Contains("docs_root", aoDesigner);
        Assert.Contains("referencePackManifest", aoDesigner);
        Assert.DoesNotContain("../../../../../docs/", aoDesigner);
        Assert.DoesNotContain("docs/en/architecture/ask-user-design.md", aoDesigner);
        Assert.Contains("Runtime Documentation References", soSkill);
        Assert.Contains("docs_root", soSkill);
        Assert.Contains("runtime_doc_resolution_policy", soSkill);
        Assert.Contains("docs_root", soDesigner);
        Assert.Contains("referencePackManifest", soDesigner);
        Assert.DoesNotContain("../../../../../docs/", soDesigner);
        Assert.Contains("runtime_doc_resolution_policy", markdownReviewer);
        Assert.Contains("runtimeDocResolutionPolicyVerified", findingsAggregator);

        foreach (var agentPath in Directory.GetFiles(Path.Combine(soRoot, "assets", "agents"), "*.agent.md"))
        {
            Assert.DoesNotContain("../../../../../docs/", File.ReadAllText(agentPath));
        }

        Assert.Contains(".agents/skills/loom-skill-enhancement/reference/producer-output-evidence-contract.md", bundleFiles);
    }

    [Fact]
    public void GovernedAskUserTemplatesProvideAutopilotEntryHints()
    {
        var root = FindRepositoryRoot();
        var templates = new[]
        {
            (
                Path: Path.Combine(root, ".agents", "skills", "loom-plan-execution", "assets", "so-workflow", "so-template.json"),
                TransitionId: "transition.collect_governance_scope"
            ),
            (
                Path: Path.Combine(root, ".agents", "skills", "loom-skill-enhancement", "assets", "so-workflow", "so-template.json"),
                TransitionId: "transition.request_review"
            ),
        };

        foreach (var template in templates)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(template.Path));
            var transition = document.RootElement.GetProperty("nodes").GetProperty(template.TransitionId);
            Assert.Equal("askUser", transition.GetProperty("stepKind").GetString());
            var authoredHint = transition.GetProperty("command").GetProperty("parameters").GetProperty("skillHint").GetString() ?? string.Empty;
            Assert.False(string.IsNullOrWhiteSpace(authoredHint));
            var workflow = WorkflowJsonSerializer.Deserialize(File.ReadAllText(template.Path));
            var pending = Assert.IsType<CommandTransition>(workflow.Nodes[template.TransitionId]);
            var hint = Assert.IsType<string>(AskScopedSubmissionWorkflow.CreateBoundaryHint(pending, pending.Description));
            Assert.Contains(authoredHint, hint, StringComparison.Ordinal);
            foreach (var term in new[]
                     {
                         "autopilot",
                         "ask_user_endpoints",
                         "HTTP session/submission flow",
                         "agent-generated",
                         "not user-confirmed",
                         "additional-context",
                         "skip the interactive ask",
                         "workflow remains waiting",
                         "validated receipt",
                         "genuine human approval",
                         "do not submit an agent-generated approval via HTTP",
                         "consume the active session",
                         "embedded-browser",
                         "approved HTTPS reverse-proxy candidate",
                         "Never rewrite a hostname",
                     })
            {
                Assert.Contains(term, hint, StringComparison.OrdinalIgnoreCase);
            }

            Assert.True(
                hint.Contains("never mutate an active contract", StringComparison.OrdinalIgnoreCase)
                || hint.Contains("never try to modify an active ask contract", StringComparison.OrdinalIgnoreCase),
                "The hint must forbid modifying an active AskUser contract.");
        }
    }

    [Fact]
    public void AskUserSkill_UnattendedFallbackDoesNotInventASubmittedSession()
    {
        var root = Path.Combine(FindRepositoryRoot(), ".agents", "skills", "loom-ask-user");
        var skill = File.ReadAllText(Path.Combine(root, "SKILL.md"));
        var terms = new[]
        {
            "apply this branch before the numbered interactive route",
            "do not start an interactive ask",
            "Without a submitted session there is no runtime receipt or server validation",
            "do not fabricate either",
            "never mutate an active contract",
            "do not submit an agent-generated approval via HTTP",
            "not confirmed by the user"
        };
        foreach (var term in terms)
        {
            Assert.Contains(term, skill, StringComparison.OrdinalIgnoreCase);
        }

        var semantics = File.ReadAllText(Path.Combine(root, "reference", "answer-semantics.md"));
        Assert.Contains("no runtime receipt or server validation", semantics, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PlanExecutionSkill_AppliesUnattendedPolicyBeforeInteractiveAsk()
    {
        var path = Path.Combine(FindRepositoryRoot(), ".agents", "skills", "loom-plan-execution", "SKILL.md");
        var skill = File.ReadAllText(path);
        foreach (var term in new[]
                 {
                     "host explicitly identifies autopilot",
                     "before acquiring a runtime for the ask or invoking a blocking question tool",
                     "same active runtime-returned endpoint",
                     "HTTP session/submission flow",
                     "unattended direct answers have no receipt",
                     "agent-generated, user-unconfirmed warning",
                     "additional-context",
                     "never mutate an active contract",
                     "without fabricating a receipt or approval",
                     "leave the workflow or human approval waiting",
                     "Outside autopilot",
                 })
        {
            Assert.Contains(term, skill, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Enhancement_PropagatesAutopilotAskPolicyAcrossTargetSurfaces()
    {
        var skillRoot = Path.Combine(FindRepositoryRoot(), ".agents", "skills", "loom-skill-enhancement");
        var skill = File.ReadAllText(Path.Combine(skillRoot, "SKILL.md"));
        var skillTerms = new[]
        {
            "own `SKILL.md`",
            "target-local agent",
            "caller-facing hint",
            "host explicitly",
            "blocking user-input tool",
            "ask_user_endpoints",
            "HTTP session/submission",
            "agent generated",
            "not user-confirmed",
            "additional-context",
            "never mutate an active contract",
            "Never fabricate a workflow receipt",
            "genuine human approval",
            "do not submit an agent-generated approval via HTTP",
            "leave the workflow waiting",
            "Outside autopilot"
        };
        foreach (var term in skillTerms)
        {
            Assert.Contains(term, skill, StringComparison.OrdinalIgnoreCase);
        }

        var agents = Path.Combine(skillRoot, "assets", "agents");
        var markdown = File.ReadAllText(Path.Combine(agents, "loom-skill-enhancement-skill-markdown-gap-review.agent.md"));
        var callers = File.ReadAllText(Path.Combine(agents, "loom-skill-enhancement-workflow-governance-gap-review.agent.md"));
        var repair = File.ReadAllText(Path.Combine(agents, "loom-skill-enhancement-review-fix-loop.agent.md"));
        var aggregate = File.ReadAllText(Path.Combine(agents, "loom-skill-enhancement-review-findings-aggregator.agent.md"));
        var designer = File.ReadAllText(Path.Combine(agents, "workflow-designer-03-wording.md"));
        var markdownTerms = new[]
        {
            "autopilot_ask_policy",
            "host_identified",
            "same_session_http",
            "receipt_and_approval",
            "current repaired",             "no active-contract mutation"
        };
        foreach (var term in markdownTerms)
        {
            Assert.Contains(term, markdown, StringComparison.OrdinalIgnoreCase);
        }

        var callerTerms = new[]
        {
            "autopilot_ask_surfaces",
            "surfaceEvidenceFile",
            "loom.autopilot.ask-surfaces.v1",
            "noApplicableSurfacesReason",
            "every workflow",
            ".agent.md",             "no active-contract mutation"
        };
        foreach (var term in callerTerms)
        {
            Assert.Contains(term, callers, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("post_fix_review_sources_file", repair);
        Assert.Contains("loom.autopilot.review-sources.v1", repair);
        Assert.Contains("autopilotAskPolicyVerified", aggregate);
        Assert.Contains("autopilotAskSurfacesVerified", aggregate);
        Assert.Contains("does not independently compare", aggregate);
        Assert.Contains("Every skill being enhanced", designer);
    }

    [Fact]
    public void EnhancementPostFixGate_RequiresPolicySurfaceCoverageAndTrustedAttestations()
    {
        static IDictionary<string, object?> Obj(object? value) => Assert.IsAssignableFrom<IDictionary<string, object?>>(value);
        static IDictionary<string, object?> Auto(IDictionary<string, object?> context) => Obj(Obj(context["post_fix_skill_markdown_validation"])["autopilot_ask_policy"]);
        static IDictionary<string, object?> Surfaces(IDictionary<string, object?> context) => Obj(Obj(context["post_fix_workflow_governance_validation"])["autopilot_ask_surfaces"]);
        static Dictionary<string, object?> Policy() => new(StringComparer.Ordinal)
        {
            ["verdict"] = "pass",
            ["missingRequirements"] = Array.Empty<string>(),
            ["missingRequirementCount"] = 0L,
            ["evidence"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["path"] = "SKILL.md",
                ["startLine"] = 1L,
                ["endLine"] = 2L,
                ["quote"] = "policy evidence"
            }
        };
        static Dictionary<string, object?> Context() => new(StringComparer.Ordinal)
        {
            ["batch_repair_evidence"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["post_fix_review_sources_file"] = Path.Combine(Path.GetTempPath(), "post-fix-review-sources.json")
            },
            ["aggregated_post_fix_validation"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["status"] = "complete",
                ["evidenceQuoteVerified"] = true,
                ["runtimeDocResolutionPolicyVerified"] = true,
                ["autopilotAskPolicyVerified"] = true,
                ["autopilotAskSurfacesVerified"] = true
            },
            ["post_fix_skill_markdown_validation"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["selective_json_policy"] = Policy(),
                ["runtime_doc_resolution_policy"] = Policy(),
                ["autopilot_ask_policy"] = Policy()
            },
            ["post_fix_workflow_governance_validation"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["autopilot_ask_surfaces"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["verdict"] = "pass",
                    ["applicableSurfaceCount"] = 1L,
                    ["reviewedSurfaceCount"] = 1L,
                    ["missingRequirementCount"] = 0L,
                    ["surfaceEvidenceFile"] = Path.Combine(Path.GetTempPath(), "ask-surfaces.json"),
                    ["noApplicableSurfacesReason"] = null
                }
            }
        };
        var path = Path.Combine(FindRepositoryRoot(), ".agents", "skills", "loom-skill-enhancement", "assets", "so-workflow", "so-template.json");
        var workflow = WorkflowJsonSerializer.Deserialize(File.ReadAllText(path));
        var transition = Assert.IsType<CommandTransition>(workflow.Nodes["transition.aggregate_post_fix_validation"]);
        var evaluator = new CSharpExpressionEvaluator(workflow.ExpressionBinding);
        var source = transition.SucceedExpression.Source;
        Assert.True(evaluator.EvaluateBoolean(source, Context()));
        var cases = new (string Name, Action<Dictionary<string, object?>> Mutate)[]
        {
            ("missing policy", c => Obj(c["post_fix_skill_markdown_validation"]).Remove("autopilot_ask_policy")),
            ("failed policy", c => Auto(c)["verdict"] = "fail"),
            ("missing requirement", c => Auto(c)["missingRequirements"] = new[] { "same_session_http" }),
            ("nonzero missing count", c => Auto(c)["missingRequirementCount"] = 1L),
            ("wrong evidence path", c => Obj(Auto(c)["evidence"])["path"] = "other.md"),
            ("invalid evidence range", c => Obj(Auto(c)["evidence"])["endLine"] = 0L),
            ("empty evidence quote", c => Obj(Auto(c)["evidence"])["quote"] = ""),
            ("failed caller review", c => Surfaces(c)["verdict"] = "fail"),
            ("unreviewed caller", c => Surfaces(c)["reviewedSurfaceCount"] = 0L),
            ("negative caller count", c => Surfaces(c)["applicableSurfaceCount"] = -1L),
            ("missing caller report", c => Surfaces(c).Remove("surfaceEvidenceFile")),
            ("empty caller report", c => Surfaces(c)["surfaceEvidenceFile"] = ""),
            ("missing caller requirements", c => Surfaces(c)["missingRequirementCount"] = 1L),
            ("missing source manifest", c => Obj(c["batch_repair_evidence"]).Remove("post_fix_review_sources_file")),
            ("empty source manifest", c => Obj(c["batch_repair_evidence"])["post_fix_review_sources_file"] = ""),
            ("policy not verified", c => Obj(c["aggregated_post_fix_validation"])["autopilotAskPolicyVerified"] = false),
            ("callers not verified", c => Obj(c["aggregated_post_fix_validation"])["autopilotAskSurfacesVerified"] = false),
            ("runtime docs not verified", c => Obj(c["aggregated_post_fix_validation"])["runtimeDocResolutionPolicyVerified"] = false),
            ("selective policy fails", c => Obj(Obj(c["post_fix_skill_markdown_validation"])["selective_json_policy"])["verdict"] = "fail")
        };
        foreach (var scenario in cases)
        {
            var context = Context();
            scenario.Mutate(context);
            Assert.False(evaluator.EvaluateBoolean(source, context), scenario.Name);
        }

        var none = Context();
        var surfaces = Surfaces(none);
        surfaces["applicableSurfaceCount"] = 0L;
        surfaces["reviewedSurfaceCount"] = 0L;
        surfaces["surfaceEvidenceFile"] = Path.Combine(Path.GetTempPath(), "no-callers.json");
        surfaces["noApplicableSurfacesReason"] = "The skill has no ask callers.";
        Assert.True(evaluator.EvaluateBoolean(source, none));
        surfaces["noApplicableSurfacesReason"] = "";
        Assert.False(evaluator.EvaluateBoolean(source, none));
        var many = Context();
        Obj(many["batch_repair_evidence"])["changed_files"] = Enumerable.Range(0, 128).Select(index => $"file-{index}.md").ToArray();
        Surfaces(many)["applicableSurfaceCount"] = 128L;
        Surfaces(many)["reviewedSurfaceCount"] = 128L;
        Assert.True(evaluator.EvaluateBoolean(source, many));

        var trusted = Context();
        Obj(Auto(trusted)["evidence"])["quote"] = "not independently checked against disk";
        Assert.True(evaluator.EvaluateBoolean(source, trusted));
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

    [Theory]
    [InlineData("loom-plan-execution", 7)]
    [InlineData("loom-skill-enhancement", 9)]
    public void DesignerContract_IsCompactCompleteAndPortable(string skill, int expectedDocumentCount)
    {
        var root = FindRepositoryRoot();
        var skillRoot = Path.Combine(root, ".agents", "skills", skill);
        var agentPath = Path.Combine(skillRoot, "assets", "agents", $"{skill}-workflow-designer.agent.md");
        var documents = DesignerContractDocuments(agentPath);
        Assert.Equal(expectedDocumentCount, documents.Length);
        Assert.Equal(documents.Length, documents.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            Directory.GetFiles(Path.GetDirectoryName(agentPath)!, "workflow-designer-*.md").Order(StringComparer.Ordinal),
            documents.Order(StringComparer.Ordinal));
        var entrypoint = File.ReadAllText(agentPath);
        Assert.StartsWith($"---\nname: {skill} Workflow Designer\ndescription: ", entrypoint.Replace("\r\n", "\n", StringComparison.Ordinal));
        Assert.Contains("Read every document in the listed order", entrypoint);
        Assert.Contains("missing or unreadable", entrypoint);
        Assert.Contains("resolved entrypoint path", entrypoint);
        var fallback = entrypoint[entrypoint.IndexOf("## Missing Reference Fallback", StringComparison.Ordinal)..];
        Assert.Contains($"`{{skillname}}` is `{skill}`", fallback);
        Assert.Contains("First try the exact relative path", fallback);
        Assert.Contains("same skill-relative path under `~/.agents/skills/{skillname}/`", fallback);
        Assert.Contains("~/.claude/skills/{skillname}/", fallback);
        Assert.Contains("supported by the current agent", fallback);
        Assert.Contains("matches the active copy's declared version", fallback);
        Assert.Contains("Record the original missing path, attempted roots, and exact selected path", fallback);
        Assert.Contains("report the attempted paths and stop the dependent work", fallback);
        Assert.Contains("never replaces the selected runtime's authoritative", fallback);
        Assert.Contains("Missing Reference Fallback", File.ReadAllText(Path.Combine(skillRoot, "SKILL.md")));

        using var releaseSet = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "release-set.json")));
        var bundleFiles = releaseSet.RootElement.GetProperty("surfaces").GetProperty("skill_bundle_files")
            .EnumerateArray().Select(item => item.GetString()).ToArray();
        foreach (var file in documents.Prepend(agentPath))
        {
            Assert.InRange(File.ReadAllLines(file).Length, 1, 199);
            Assert.Contains(Path.GetRelativePath(root, file).Replace('\\', '/'), bundleFiles);
        }

        var installedRoot = Path.Combine(Path.GetTempPath(), $"loom-designer-contract-{Guid.NewGuid():N}");
        try
        {
            foreach (var source in Directory.EnumerateFiles(skillRoot, "*", SearchOption.AllDirectories))
            {
                var destination = Path.Combine(installedRoot, Path.GetRelativePath(skillRoot, source));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(source, destination);
            }
            var installedAgent = Path.Combine(installedRoot, Path.GetRelativePath(skillRoot, agentPath));
            Assert.Equal(ReadDesignerContract(agentPath), ReadDesignerContract(installedAgent));
            foreach (var file in DesignerContractDocuments(installedAgent).Prepend(installedAgent))
            {
                foreach (Match link in Regex.Matches(File.ReadAllText(file), @"\[[^\]\r\n]+\]\(([^)\r\n]+)\)"))
                {
                    var target = link.Groups[1].Value.Split('#')[0];
                    Assert.True(target.StartsWith("./", StringComparison.Ordinal) || target.StartsWith("../", StringComparison.Ordinal),
                        $"Expected a skill-relative link in {file}: {target}");
                    var resolved = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file)!, target));
                    Assert.StartsWith(installedRoot + Path.DirectorySeparatorChar, resolved);
                    Assert.True(File.Exists(resolved), $"Missing installed contract reference: {resolved}");
                }
            }
            var missingDocument = DesignerContractDocuments(installedAgent)[0];
            File.Delete(missingDocument);
            Assert.Throws<FileNotFoundException>(() => ReadDesignerContract(installedAgent));
        }
        finally
        {
            if (Directory.Exists(installedRoot))
            {
                Directory.Delete(installedRoot, recursive: true);
            }
        }
    }

    private static string[] DesignerContractDocuments(string agentPath)
    {
        return Regex.Matches(File.ReadAllText(agentPath), @"^- \[[^\]\r\n]+\]\(\./(workflow-designer-\d{2}-[a-z-]+\.md)\)\r?$", RegexOptions.Multiline)
            .Select(match => Path.Combine(Path.GetDirectoryName(agentPath)!, match.Groups[1].Value))
            .ToArray();
    }

    private static string ReadDesignerContract(string agentPath)
    {
        var documents = DesignerContractDocuments(agentPath);
        Assert.NotEmpty(documents);
        return File.ReadAllText(agentPath) + string.Concat(documents.Select(File.ReadAllText));
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
