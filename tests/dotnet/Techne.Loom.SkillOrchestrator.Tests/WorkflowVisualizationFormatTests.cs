using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.SkillOrchestrator.Analysis;
using Techne.Loom.SkillOrchestrator.Visualizer;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class WorkflowVisualizationFormatTests
{
    [Fact]
    public async Task AllSkillVisualizationFormatsExposeSemanticLegend()
    {
        var instance = CreateWorkflow();

        var mermaid = await new MermaidWorkflowInstanceVisualizer().VisualizeToStringAsync(instance);
        var html = await new HtmlWorkflowInstanceVisualizer().VisualizeToStringAsync(instance);
        var svg = await new SvgWorkflowInstanceVisualizer().VisualizeToStringAsync(instance);
        var ascii = await new AsciiArtWorkflowInstanceVisualizer().VisualizeToStringAsync(instance);

        Assert.Contains("subgraph legend[Legend]", mermaid);
        Assert.Contains("Code/Tool", mermaid);
        Assert.Contains("wf-legend", html);
        Assert.Contains("Runtime / tool", html);
        Assert.Contains("<g id=\"legend\">", svg);
        Assert.Contains("Legend:", ascii);
        Assert.Contains("Runtime / tool", ascii);
        Assert.Contains("Done", mermaid);
        Assert.Contains("data-semantic=\"Completion\"", html);
        Assert.Contains("Done", html);
        Assert.Contains("data-semantic=\"Completion\"", svg);
        Assert.Contains("Completion: State Done [Completion]", ascii);
    }

    [Fact]
    public void BusinessSummaryRenderer_UsesBusinessLabelsAndEscapesTableCells()
    {
        var instance = CreateWorkflow();
        var start = (StateNode)instance.Nodes["state.start"];
        start.Name = "CK1 - Concept direction review";
        start.Description = "[review](https://example.invalid) ![diagram](https://example.invalid/x.png) `inline` *bold* _italic_ ~~strike~~ | note\nnext";

        var markdown = new WorkflowBusinessSummaryMarkdownRenderer().Render(instance);

        Assert.Contains("## Workflow Business Summary", markdown);
        Assert.Contains("Phase", markdown);
        Assert.Contains("CK1 - Concept direction review", markdown);
        Assert.Contains("Not provided", markdown);
        Assert.Contains("\\[review\\]\\(https://example.invalid\\)", markdown);
        Assert.Contains("\\!\\[diagram\\]\\(https://example.invalid/x.png\\)", markdown);
        Assert.Contains("\\`inline\\`", markdown);
        Assert.Contains("\\*bold\\*", markdown);
        Assert.Contains("\\_italic\\_", markdown);
        Assert.Contains("\\~\\~strike\\~\\~", markdown);
        Assert.Contains("\\| note next", markdown);
    }

    [Fact]
    public void CompileAuditHtmlRenderer_ShowsProvenanceDiagnosticsAndEscapedEvidence()
    {
        var instance = CreateWorkflow();
        instance.CaseId = "case-audit-1";
        instance.RunId = "run-audit-1";
        ((StateNode)instance.Nodes["state.start"]).Description = "Review <script>alert('x')</script>";
        var feedback = new WorkflowCompileFeedback
        {
            Product = "skill-orchestrator",
            Runtime = "dotnet-so",
            RuntimeIdentity = "Techne.Loom.SkillOrchestrator",
            RuntimeVersion = "1.2.3",
            WorkflowPath = "C:/workflows/sample.json",
            WorkflowHash = "abcdef",
            Counts = new WorkflowCompileFeedbackCounts { Total = 1, Warnings = 1 },
            Truncated = true,
            Diagnostics =
            [
                new WorkflowCompileDiagnostic
                {
                    RuleId = "workflow.label",
                    Code = "LOOM.TEST.LABEL",
                    Category = "authoring",
                    BlockedBy = ["compile.phase.expressions"],
                    Severity = "warning",
                    Message = "Review <script> label",
                    Location = "nodes.state.start.name",
                    SuggestedFix = "Use a concise business label.",
                    Phase = "semantic",
                    ExpressionFeedback = new ExpressionCompileFeedback
                    {
                        Status = "failed",
                        Language = "C#",
                        LanguageVersion = "12",
                        ContractId = "expr.v1",
                        ContractVersion = "1",
                        WorkflowId = "case-audit-1",
                        GateId = "gate.review",
                        TransitionId = "transition.tool",
                        Field = "validation.gates.review.passExpression",
                        SourceSpan = new ExpressionSourceSpan { StartLine = 2, StartColumn = 3, EndLine = 2, EndColumn = 8 },
                        DiagnosticCode = "EXPR001",
                        DiagnosticCategory = "policy",
                        Severity = "error",
                        Message = "Root expression feedback message",
                        SuggestedFix = "Use a supported expression.",
                        CompilerIdentity = "Roslyn",
                        Kind = "predicate",
                        EntryPoint = "Evaluate",
                        ResultType = "bool",
                        ReferencedSymbols = ["WorkflowContext.Has"],
                        Capabilities = ["context-read"],
                        Warnings = ["Expression warning detail"],
                        Truncated = true,
                        DiagnosticCount = 2,
                        Diagnostics = [new ExpressionCompileDiagnostic { Code = "EXPR001", Category = "policy", Severity = "error", Message = "Nested expression diagnostic detail", SuggestedFix = "Use the supported expression capability.", SourceSpan = new ExpressionSourceSpan { StartLine = 1, StartColumn = 2, EndLine = 1, EndColumn = 5 } }],
                    },
                },
            ],
            Phases = [new WorkflowCompilePhaseFeedback { Name = "semantic", Status = "completed", DiagnosticCount = 1 }],
        };
        instance.Validation = new WorkflowValidationContract
        {
            Gates = new Dictionary<string, WorkflowValidationGate>(StringComparer.Ordinal)
            {
                ["gate.review"] = new WorkflowValidationGate
                {
                    Description = "Review package completeness",
                    PassExpression = new ExpressionDefinition { Source = "context.Has(\"assessment\")", ResultType = "bool" },
                    RequiredOutputFamilies = ["assessment"],
                    RequiredMachineReadableOutputFamilies = ["assessment.json"],
                    RequiredHumanReviewableOutputFamilies = ["assessment.report.md"],
                    ValueSemantics = new Dictionary<string, string>(StringComparer.Ordinal) { ["assessment"] = "non-empty JSON" },
                    InstanceBinding = "caseId/runId",
                    FailureGuidance = new WorkflowGateFailureGuidance
                    {
                        Summary = "Assessment is missing",
                        NextAction = "Create the assessment artifact",
                        EvidenceReferences = [new WorkflowEvidenceReference { Path = "docs/review.md", StartLine = 4, EndLine = 5, Quote = "Review required fields." }],
                    },
                },
            },
            Routes = new Dictionary<string, WorkflowRouteValidationProfile>(StringComparer.Ordinal)
            {
                ["route.assessment"] = new WorkflowRouteValidationProfile
                {
                    Description = "Assessment completion route",
                    RequiredTerminalGateIds = ["gate.review"],
                    RequiredBlockedGateIds = ["gate.review"],
                },
            },
        };
        var toolTransition = Assert.IsType<CommandTransition>(instance.Nodes["transition.tool"]);
        instance.Nodes[toolTransition.Id] = toolTransition with
        {
            SatisfiesGateIds = ["gate.review"],
            TerminalRoutes = ["route.assessment"],
            BlockedRoutes = ["route.blocked"],
        };
        var analysis = new SkillWorkflowAnalyzer().Analyze(instance) with
        {
            Branches = [new WorkflowBranchAnalysis("state.start", "group.start", ["transition.tool", "transition.review"], ["context.Has(\"assessment\")"], false)],
        };
        var html = new WorkflowCompileAuditHtmlRenderer().Render(instance, feedback, analysis);
        Assert.Contains("Compile Audit", html);
        Assert.Contains("<span lang=\"zh-CN\">编译审计</span>", html);
        Assert.Contains("<span lang=\"zh-CN\">诊断</span>", html);
        Assert.Contains("Workflow SHA-256", html);
        Assert.Contains("Runtime identity", html);
        Assert.Contains("Techne.Loom.SkillOrchestrator", html);
        Assert.Contains("case-audit-1", html);
        Assert.Contains("Diagnostics", html);
        Assert.Contains("Incomplete diagnostic detail", html);
        Assert.Contains("Use a concise business label.", html);
        Assert.Contains("Dataflow evidence", html);
        Assert.Contains("Info", html);
        Assert.Contains("Analyzer control-risk flag", html);
        Assert.Contains("Step-kind counts", html);
        Assert.Contains("Requested input fields", html);
        Assert.Contains("Published output families", html);
        Assert.Contains("Projection and resume contract", html);
        Assert.Contains("Produced context paths", html);
        Assert.Contains("Published output families", html);
        Assert.Contains("Unresolved output families", html);
        Assert.Contains("Declared ownership", html);
        Assert.Contains("transition.tool, transition.review", html);
        Assert.Contains("Review package completeness", html);
        Assert.Contains("context.Has(&quot;assessment&quot;)", html);
        Assert.Contains("caseId/runId", html);
        Assert.Contains("assessment.json", html);
        Assert.Contains("non-empty JSON", html);
        Assert.Contains("Create the assessment artifact", html);
        Assert.Contains("docs/review.md:4-5 Review required fields.", html);
        Assert.Contains("Category", html);
        Assert.Contains("authoring", html);
        Assert.Contains("compile.phase.expressions", html);
        Assert.Contains("2:3-2:8", html);
        Assert.Contains("Blocked by", html);
        Assert.Contains("Root expression feedback message", html);
        Assert.Contains("gate=gate.review", html);
        Assert.Contains("validation.gates.review.passExpression", html);
        Assert.Contains("Nested expression diagnostic detail", html);
        Assert.Contains("Nested expression diagnostics are truncated", html);
        Assert.Contains("EXPR001", html);
        Assert.Contains("1:2-1:5", html);
        Assert.Contains("route.assessment", html);
        Assert.Contains("Assessment completion route", html);
        Assert.Contains("Terminal gates", html);
        Assert.Contains("Blocked gates", html);
        Assert.Contains("Satisfied gates", html);
        Assert.Contains("Terminal routes", html);
        Assert.Contains("route.assessment", html);
        Assert.Contains("Blocked routes", html);
        Assert.Contains("route.blocked", html);
        Assert.Contains("Dataflow route names", html);
        Assert.Contains("kind=predicate", html);
        Assert.Contains("entryPoint=Not declared", html);
        Assert.Contains("resultType=bool", html);
        Assert.Contains("Human-reviewable outputs", html);
        Assert.Contains("assessment.report.md", html);
        Assert.Contains("Input paths", html);
        Assert.Contains("Payload paths", html);
        Assert.Contains("Produced context paths", html);
        Assert.Contains("Published output families", html);
        Assert.Contains("Unresolved output families", html);
        Assert.Contains("&lt;script&gt;", html);
        Assert.DoesNotContain("<script>alert('x')</script>", html);
        Assert.Contains("@media(max-width:640px)", html);
    }

    private static WorkflowInstance CreateWorkflow()
    {
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            WorkflowPhase = "01 Intake",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.start",
                    Strategy = ConcurrencyStrategy.FirstSuccess,
                    TransitionIds = ["transition.tool"],
                },
            ],
        };
        var done = new StateNode
        {
            Id = "state.done",
            Name = "Done",
            WorkflowPhase = "02 Complete",
            Groups = [],
        };
        var tool = new CommandTransition
        {
            Id = "transition.tool",
            Name = "Echo",
            TargetNodeId = "state.done",
            StepKind = WorkflowStepKind.ToolCall,
            GuardExpression = "true",
            SucceedExpression = "true",
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "echo",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["message"] = "hello",
                },
            },
        };

        return new WorkflowInstance
        {
            InstanceId = "visualization-format-test",
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = done.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [done.Id] = done,
                [tool.Id] = tool,
            },
        };
    }
}
