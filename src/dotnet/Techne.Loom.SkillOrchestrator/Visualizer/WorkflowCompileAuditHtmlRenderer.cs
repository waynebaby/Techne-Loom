using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.SkillOrchestrator.Analysis;

namespace Techne.Loom.SkillOrchestrator.Visualizer;

public sealed class WorkflowCompileAuditHtmlRenderer
{
    public string Render(WorkflowInstance workflow, WorkflowCompileFeedback feedback, SkillWorkflowAnalysisReport analysis)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(feedback);
        ArgumentNullException.ThrowIfNull(analysis);

        var builder = new StringBuilder();
        var statusClass = string.Equals(feedback.Status, "succeeded", StringComparison.OrdinalIgnoreCase) ? "status-pass" : "status-fail";
        builder.AppendLine("<!doctype html>");
        builder.AppendLine("<html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>Workflow compile audit</title>");
        builder.AppendLine("<style>");
        builder.AppendLine(Styles);
        builder.AppendLine("</style></head><body>");
        builder.AppendLine("<main class=\"report\">");
        builder.AppendLine("<header class=\"report-header\"><p class=\"eyebrow\">TECHNE LOOM / WORKFLOW VALIDATION</p><h1>Compile Audit / 编译审计</h1>");
        builder.AppendLine($"<p class=\"status {statusClass}\"><span class=\"status-mark\" aria-hidden=\"true\">{(statusClass == "status-pass" ? "✓" : "!")}</span>{E(feedback.Status)} / {(statusClass == "status-pass" ? "通过" : "未通过")}</p>");
        builder.AppendLine("<dl class=\"identity\">");
        AddFact(builder, "Workflow / 工作流", workflow.InstanceId);
        AddFact(builder, "Product / 产品", feedback.Product);
        AddFact(builder, "Runtime / 运行时", feedback.Runtime);
        AddFact(builder, "Runtime identity / 运行时标识", feedback.RuntimeIdentity);
        AddFact(builder, "Runtime version / 运行时版本", feedback.RuntimeVersion);
        AddFact(builder, "Workflow source / 工作流来源", feedback.WorkflowPath);
        AddFact(builder, "Workflow SHA-256 / 工作流哈希", feedback.WorkflowHash, "hash");
        AddFact(builder, "Case ID", workflow.CaseId);
        AddFact(builder, "Run ID", workflow.RunId);
        builder.AppendLine("</dl></header>");

        builder.AppendLine("<section class=\"summary-grid\" aria-label=\"Compile counts\">");
        AddMetric(builder, "States / 状态", analysis.StateCount);
        AddMetric(builder, "Transitions / 转移", analysis.TransitionCount);
        AddMetric(builder, "Diagnostics / 诊断", feedback.Counts.Total);
        AddMetric(builder, "Errors / 错误", feedback.Counts.Errors);
        AddMetric(builder, "Warnings / 警告", feedback.Counts.Warnings);
        AddMetric(builder, "Info / 信息", feedback.Counts.Info);
        AddMetric(builder, "Blocked / 阻断", feedback.Counts.Blocked);
        AddMetric(builder, "Dataflow issues / 数据流问题", analysis.Dataflow.Issues.Count);
        builder.AppendLine("</section>");

        AppendDiagnostics(builder, feedback);
        AppendControlFlow(builder, workflow, analysis);
        AppendDataflow(builder, workflow, analysis.Dataflow);
        AppendWorkflowDetails(builder, workflow);
        builder.AppendLine("<footer>Evidence source / 证据来源: this report summarizes the workflow and compile artifacts recorded for this compile. / 本报告仅汇总本次编译记录的工作流和编译产物。</footer>");
        builder.AppendLine("</main></body></html>");
        return MarkChineseText(builder.ToString());
    }

    private static void AppendDiagnostics(StringBuilder builder, WorkflowCompileFeedback feedback)
    {
        builder.AppendLine("<details class=\"section\" open><summary><span>Diagnostics / 诊断</span><span class=\"count\">" + feedback.Diagnostics.Count + "</span></summary><div class=\"section-body\">");
        if (feedback.Truncated)
        {
            builder.AppendLine("<p class=\"issues\" role=\"note\"><strong>Incomplete diagnostic detail / 诊断信息不完整:</strong> The compile feedback marks diagnostic content as truncated. This report shows only the diagnostics present in the recorded feedback and should not be treated as a complete diagnostic list. / 编译反馈标记诊断内容已截断。本报告只展示记录中的诊断项，不应视为完整诊断清单。</p>");
        }
        if (feedback.Diagnostics.Count == 0)
        {
            builder.AppendLine("<p class=\"empty\">No diagnostics were recorded. / 未记录诊断信息。</p>");
        }
        else
        {
            builder.AppendLine("<div class=\"table-wrap\"><table><thead><tr><th>Severity / 级别</th><th>Category / 类别</th><th>Phase / 阶段</th><th>Code / 代码</th><th>Location / 位置</th><th>Blocked by / 阻断原因</th><th>Message and evidence / 信息与证据</th></tr></thead><tbody>");
            foreach (var diagnostic in feedback.Diagnostics)
            {
                builder.Append("<tr><td><span class=\"severity\">").Append(E(diagnostic.Severity)).Append("</span></td><td>").Append(E(diagnostic.Category)).Append("</td><td>").Append(E(diagnostic.Phase)).Append("</td><td><code>").Append(E(diagnostic.Code)).Append("</code><br><small>").Append(E(diagnostic.RuleId)).Append("</small></td><td><code>").Append(E(diagnostic.Location)).Append("</code></td><td>").Append(E(string.Join(", ", diagnostic.BlockedBy))).Append("</td><td><p>").Append(E(diagnostic.Message)).AppendLine("</p>");
                if (!string.IsNullOrWhiteSpace(diagnostic.SuggestedFix))
                {
                    builder.Append("<p class=\"suggestion\"><strong>Suggested fix / 修复建议:</strong> ").Append(E(diagnostic.SuggestedFix)).AppendLine("</p>");
                }
                if (diagnostic.ExpressionFeedback is { } expression)
                {
                    builder.Append("<details><summary>Expression feedback / 表达式反馈 · ").Append(E(expression.Status)).Append(" · ").Append(expression.DiagnosticCount).AppendLine(" diagnostics</summary>");
                    if (expression.Truncated)
                    {
                        builder.AppendLine("<p class=\"issues\" role=\"note\">Nested expression diagnostics are truncated. / 嵌套表达式诊断已截断。</p>");
                    }
                    builder.Append("<p><strong>Owner / 所属:</strong> workflow=").Append(E(expression.WorkflowId)).Append(" · gate=").Append(E(expression.GateId)).Append(" · transition=").Append(E(expression.TransitionId)).AppendLine("</p>");
                    builder.Append("<p><code>").Append(E(expression.Field)).Append("</code> · ").Append(E(expression.Language)).Append(" ").Append(E(expression.LanguageVersion)).Append(" · ").Append(E(expression.ContractId)).Append("/").Append(E(expression.ContractVersion)).Append(" · compiler ").Append(E(expression.CompilerIdentity)).AppendLine("</p>");
                    builder.Append("<p><strong>").Append(E(expression.Severity)).Append(" ").Append(E(expression.DiagnosticCode)).Append(" / ").Append(E(expression.DiagnosticCategory)).Append(":</strong> ").Append(E(expression.Message)).AppendLine("</p>");
                    if (!string.IsNullOrWhiteSpace(expression.SuggestedFix))
                    {
                        builder.Append("<p class=\"suggestion\"><strong>Suggested fix / 修复建议:</strong> ").Append(E(expression.SuggestedFix)).AppendLine("</p>");
                    }
                    builder.Append("<p>kind=").Append(E(expression.Kind)).Append("; entryPoint=").Append(E(expression.EntryPoint)).Append("; resultType=").Append(E(expression.ResultType)).AppendLine("</p>");
                    if (expression.SourceSpan is { } span)
                    {
                        builder.Append("<p>Source span / 源码位置: ").Append(span.StartLine).Append(':').Append(span.StartColumn).Append("-").Append(span.EndLine).Append(':').Append(span.EndColumn).AppendLine("</p>");
                    }
                    builder.Append("<p>Referenced symbols / 引用符号: <code>").Append(E(string.Join(", ", expression.ReferencedSymbols))).AppendLine("</code></p>");
                    builder.Append("<p>Capabilities / 能力: ").Append(E(string.Join(", ", expression.Capabilities))).AppendLine("</p>");
                    foreach (var warning in expression.Warnings)
                    {
                        builder.Append("<p class=\"suggestion\">Warning / 警告: ").Append(E(warning)).AppendLine("</p>");
                    }
                    foreach (var item in expression.Diagnostics)
                    {
                        var diagnosticSpan = item.SourceSpan is { } nestedSpan
                            ? $" · {nestedSpan.StartLine}:{nestedSpan.StartColumn}-{nestedSpan.EndLine}:{nestedSpan.EndColumn}"
                            : string.Empty;
                        builder.Append("<p><span class=\"severity\">").Append(E(item.Severity)).Append("</span> <code>").Append(E(item.Code)).Append("</code> · ").Append(E(item.Category)).Append(" · ").Append(E(item.Message)).Append(diagnosticSpan).Append(" · ").Append(E(item.SuggestedFix)).AppendLine("</p>");
                    }
                    builder.AppendLine("</details>");
                }
                builder.AppendLine("</td></tr>");
            }
            builder.AppendLine("</tbody></table></div>");
        }
        builder.AppendLine("<h3>Compile phases / 编译阶段</h3><div class=\"table-wrap\"><table><thead><tr><th>Phase / 阶段</th><th>Status / 状态</th><th>Diagnostics / 诊断数</th><th>Prerequisites / 前置条件</th><th>Blocked by / 阻断原因</th></tr></thead><tbody>");
        foreach (var phase in feedback.Phases)
        {
            builder.Append("<tr><td>").Append(E(phase.Name)).Append("</td><td>").Append(E(phase.Status)).Append("</td><td>").Append(phase.DiagnosticCount).Append("</td><td>").Append(E(string.Join(", ", phase.Prerequisites))).Append("</td><td>").Append(E(string.Join(", ", phase.BlockedBy))).AppendLine("</td></tr>");
        }
        builder.AppendLine("</tbody></table></div></div></details>");
    }

    private static void AppendControlFlow(StringBuilder builder, WorkflowInstance workflow, SkillWorkflowAnalysisReport analysis)
    {
        builder.AppendLine("<details class=\"section\"><summary><span>Control flow, ownership and gates / 控制流、归属与门禁</span><span class=\"count\">" + (analysis.Branches.Count + analysis.Loops.Count + analysis.UserSeams.Count + analysis.RuntimeSeams.Count + analysis.GateIds.Count) + "</span></summary><div class=\"section-body\">");
        builder.AppendLine("<h3>Branches / 分支</h3>");
        if (analysis.Branches.Count == 0) builder.AppendLine("<p class=\"empty\">No branch structures recorded. / 未记录分支结构。</p>");
        foreach (var branch in analysis.Branches)
        {
            builder.Append("<article class=\"branch\"><p><code>").Append(E(branch.StateId)).Append(" / ").Append(E(branch.GroupId)).Append("</code> · ").Append(branch.IsSwitchLike ? "switch-like" : "conditional").AppendLine("</p>");
            builder.Append("<p><strong>Transition IDs / 转移 ID:</strong> <code>").Append(E(string.Join(", ", branch.TransitionIds))).AppendLine("</code></p>");
            builder.Append("<p><strong>Guard expressions / 条件表达式:</strong> <code>").Append(E(string.Join("; ", branch.GuardExpressions))).AppendLine("</code></p></article>");
        }
        builder.AppendLine("<h3>Loops / 循环</h3>");
        if (analysis.Loops.Count == 0) builder.AppendLine("<p class=\"empty\">No loops recorded. / 未记录循环。</p>");
        foreach (var loop in analysis.Loops)
            builder.Append("<p><code>").Append(E(loop.SourceStateId)).Append(" → ").Append(E(loop.TargetStateId)).Append("</code> via <code>").Append(E(loop.TransitionId)).Append("</code> · ").AppendLine(loop.IsSelfLoop ? "self-loop" : "cycle");
        AppendSeams(builder, "User seams / 用户交接", analysis.UserSeams);
        AppendSeams(builder, "Runtime seams / 运行时交接", analysis.RuntimeSeams);
        builder.AppendLine("<h3>Analyzer control-risk flag / 分析器控制风险标记</h3>");
        builder.AppendLine(analysis.HasTuringCompleteControlRisk
            ? "<p class=\"issues\">Present in static analysis / 静态分析中存在。 This is a risk flag, not a runtime result. / 这是风险标记，不是运行结果。</p>"
            : "<p class=\"evidence-note\">Not detected by this analysis / 本次分析未检测到。</p>");
        builder.AppendLine("<h3>Step-kind counts / 步骤类型计数</h3><div class=\"table-wrap\"><table><thead><tr><th>Step kind / 步骤类型</th><th>Count / 数量</th></tr></thead><tbody>");
        foreach (var item in analysis.StepKindCounts.OrderBy(static item => item.Key))
            builder.Append("<tr><td><code>").Append(E(item.Key.ToString())).Append("</code></td><td>").Append(item.Value).AppendLine("</td></tr>");
        builder.AppendLine("</tbody></table></div>");
        AppendStringList(builder, "Requested input fields / 请求输入字段", analysis.RequestedInputFields);
        AppendStringList(builder, "Published output families / 已发布输出族", analysis.PublishedOutputFamilies);
        builder.AppendLine("<h3>Declared ownership / 已声明的数据归属</h3>");
        AppendStringList(builder, "User-owned fields / 用户归属字段", workflow.Validation?.DeclaredUserOwnedFields ?? analysis.DeclaredUserOwnedFields);
        AppendStringList(builder, "Runtime-owned fields / 运行时归属字段", workflow.Validation?.ReservedRuntimeOwnedFields ?? analysis.ReservedRuntimeOwnedFields);
        builder.AppendLine("<h3>Gate contracts / 门禁定义</h3>");
        var gates = workflow.Validation?.Gates ?? new Dictionary<string, WorkflowValidationGate>(StringComparer.Ordinal);
        if (gates.Count == 0)
        {
            AppendStringList(builder, "Gate IDs / 门禁标识", analysis.GateIds);
        }
        else
        {
            builder.AppendLine("<div class=\"table-wrap\"><table><thead><tr><th>Gate ID / 门禁</th><th>Description / 描述</th><th>Expression contract / 表达式合同</th><th>Instance binding / 实例绑定</th><th>Required outputs / 必需输出</th><th>Machine-readable outputs / 机器可读输出</th><th>Human-reviewable outputs / 人工审阅输出</th><th>Value semantics / 值语义</th><th>Failure guidance / 失败指引</th></tr></thead><tbody>");
            foreach (var gate in gates.OrderBy(static gate => gate.Key, StringComparer.Ordinal))
            {
                var expression = gate.Value.PassExpression is null
                    ? "Not declared / 未声明"
                    : string.Join("; ", new[]
                    {
                        "kind=" + gate.Value.PassExpression.Kind,
                        "entryPoint=" + (gate.Value.PassExpression.EntryPoint ?? "Not declared"),
                        "resultType=" + gate.Value.PassExpression.ResultType,
                        "source=" + gate.Value.PassExpression.Source,
                    });
                var valueSemantics = string.Join("; ", gate.Value.ValueSemantics.Select(static item => item.Key + " = " + item.Value));
                var guidance = gate.Value.FailureGuidance;
                var guidanceText = guidance is null
                    ? "Not declared / 未声明"
                    : string.Join("; ", new[] { guidance.Summary, guidance.NextAction }
                        .Where(static value => !string.IsNullOrWhiteSpace(value))
                        .Concat(guidance.EvidenceReferences.Select(static reference => reference.Path + ":" + reference.StartLine + "-" + reference.EndLine + " " + reference.Quote)));
                builder.Append("<tr><td><code>").Append(E(gate.Key)).Append("</code></td><td>").Append(E(gate.Value.Description ?? "Not declared / 未声明")).Append("</td><td><code>").Append(E(expression)).Append("</code></td><td>").Append(E(gate.Value.InstanceBinding ?? "Not bound / 未绑定")).Append("</td><td>").Append(E(string.Join(", ", gate.Value.RequiredOutputFamilies))).Append("</td><td>").Append(E(string.Join(", ", gate.Value.RequiredMachineReadableOutputFamilies))).Append("</td><td>").Append(E(string.Join(", ", gate.Value.RequiredHumanReviewableOutputFamilies))).Append("</td><td>").Append(E(valueSemantics.Length == 0 ? "Not declared / 未声明" : valueSemantics)).Append("</td><td>").Append(E(guidanceText)).AppendLine("</td></tr>");
            }
            builder.AppendLine("</tbody></table></div>");
        }
        var routes = workflow.Validation?.Routes ?? new Dictionary<string, WorkflowRouteValidationProfile>(StringComparer.Ordinal);
        if (routes.Count > 0)
        {
            builder.AppendLine("<h3>Route-to-gate coverage / 路由与门禁覆盖</h3><div class=\"table-wrap\"><table><thead><tr><th>Route / 路由</th><th>Description / 描述</th><th>Terminal gates / 完成所需门禁</th><th>Blocked gates / 阻断所需门禁</th></tr></thead><tbody>");
            foreach (var route in routes.OrderBy(static route => route.Key, StringComparer.Ordinal))
                builder.Append("<tr><td><code>").Append(E(route.Key)).Append("</code></td><td>").Append(E(route.Value.Description ?? "Not declared / 未声明")).Append("</td><td>").Append(E(string.Join(", ", route.Value.RequiredTerminalGateIds))).Append("</td><td>").Append(E(string.Join(", ", route.Value.RequiredBlockedGateIds))).AppendLine("</td></tr>");
            builder.AppendLine("</tbody></table></div>");
        }
        builder.AppendLine("<h3>Node artifact mappings / 节点产物映射</h3><div class=\"table-wrap\"><table><thead><tr><th>Node ID</th><th>Kind / 类型</th><th>Output paths / 输出路径</th><th>Output families / 输出族</th><th>Gate IDs / 门禁</th></tr></thead><tbody>");
        foreach (var mapping in analysis.NodeArtifactMap)
            builder.Append("<tr><td><code>").Append(E(mapping.NodeId)).Append("</code></td><td>").Append(E(mapping.NodeKind)).Append("</td><td>").Append(E(string.Join(", ", mapping.OutputPaths))).Append("</td><td>").Append(E(string.Join(", ", mapping.OutputFamilies))).Append("</td><td>").Append(E(string.Join(", ", mapping.GateIds))).AppendLine("</td></tr>");
        builder.AppendLine("</tbody></table></div></div></details>");
    }

    private static void AppendDataflow(StringBuilder builder, WorkflowInstance workflow, SkillWorkflowDataflowReport dataflow)
    {
        builder.AppendLine("<details class=\"section\" open><summary><span>Dataflow evidence / 数据流证据</span><span class=\"count\">" + dataflow.Transitions.Count + " transitions · " + dataflow.Issues.Count + " issues</span></summary><div class=\"section-body\">");
        if (dataflow.Issues.Count == 0)
        {
            builder.AppendLine("<p class=\"evidence-note\">No unresolved dataflow issues were recorded by the analyzer. / 分析器未记录未解决的数据流问题。</p>");
        }
        else
        {
            builder.AppendLine("<h3>Unresolved issues / 未解决问题</h3><ul class=\"issues\">");
            foreach (var issue in dataflow.Issues)
                builder.Append("<li><code>").Append(E(issue.TransitionId ?? issue.GateId ?? "workflow")).Append("</code> · ").Append(E(issue.OutputFamily)).Append(" · ").Append(E(issue.Reason)).AppendLine("</li>");
            builder.AppendLine("</ul>");
        }
        if (dataflow.GateRequiredOutputFamilies.Count > 0)
        {
            builder.AppendLine("<h3>Gate-required output families / 门禁要求的输出族</h3><div class=\"table-wrap\"><table><thead><tr><th>Gate ID / 门禁</th><th>Required output families / 必需输出族</th></tr></thead><tbody>");
            foreach (var gate in dataflow.GateRequiredOutputFamilies.OrderBy(static gate => gate.Key, StringComparer.Ordinal))
                builder.Append("<tr><td><code>").Append(E(gate.Key)).Append("</code></td><td>").Append(E(string.Join(", ", gate.Value))).AppendLine("</td></tr>");
            builder.AppendLine("</tbody></table></div>");
        }
        builder.AppendLine("<div class=\"table-wrap\"><table><thead><tr><th>Transition ID / 转移</th><th>Step / 步骤</th><th>Source → target / 源到目标</th><th>Input paths / 输入路径</th><th>Payload paths / 载荷路径</th><th>Projection and resume contract / 投影与恢复合同</th><th>Output bindings / 输出绑定</th><th>Produced context paths / 已产出上下文路径</th><th>Published output families / 已发布输出族</th><th>Unresolved output families / 未解决输出族</th><th>Satisfied gates / 已满足门禁</th><th>Terminal routes / 完成路由</th><th>Blocked routes / 阻断路由</th><th>Dataflow route names / 数据流路由名</th><th>Emitter and tool / 产出类型与工具</th></tr></thead><tbody>");
        var transitions = workflow.GetTransitionNodes();
        foreach (var item in dataflow.Transitions)
        {
            transitions.TryGetValue(item.TransitionId, out var transition);
            var sourceIds = workflow.GetStateNodes().Values.Where(state => state.Groups.Any(group => group.TransitionIds.Contains(item.TransitionId, StringComparer.Ordinal))).Select(static state => state.Id).OrderBy(static id => id, StringComparer.Ordinal);
            var terminalRoutes = string.Join(", ", transition?.TerminalRoutes ?? []);
            var blockedRoutes = string.Join(", ", transition?.BlockedRoutes ?? []);
            var bindings = string.Join(", ", item.OutputBindings.Select(static binding => binding.Key + " ← " + binding.Value));
            var projection = string.Join(", ", new[]
            {
                string.IsNullOrWhiteSpace(item.ProjectionMode) ? null : "mode=" + item.ProjectionMode,
                string.IsNullOrWhiteSpace(item.ResumeOutputKey) ? null : "resumeKey=" + item.ResumeOutputKey,
                string.IsNullOrWhiteSpace(item.OutputPath) ? null : "outputPath=" + item.OutputPath,
            }.Where(static value => value is not null));
            var produced = item.ProducedContextPaths
                .Concat(item.PublishedOutputFamilies)
                .Concat(item.UnresolvedOutputFamilies.Select(static family => "unresolved=" + family));
            var emitter = item.EmitterKind + (string.IsNullOrWhiteSpace(item.ToolName) ? string.Empty : " / " + item.ToolName);
            var updates = item.UpdatesKeys is { Count: > 0 } ? "; updates=" + string.Join(", ", item.UpdatesKeys) : string.Empty;
            builder.Append("<tr><td><code>").Append(E(item.TransitionId)).Append("</code></td><td>").Append(E(item.StepKind.ToString())).Append("</td><td><code>").Append(E(string.Join(", ", sourceIds))).Append(" → ").Append(E(transition?.TargetNodeId)).Append("</code></td><td>").Append(E(string.Join(", ", item.InputPaths))).Append("</td><td>").Append(E(string.Join(", ", item.PayloadPaths))).Append("</td><td>").Append(E(projection)).Append("</td><td>").Append(E(bindings)).Append("</td><td>").Append(E(string.Join(", ", item.ProducedContextPaths))).Append("</td><td>").Append(E(string.Join(", ", item.PublishedOutputFamilies))).Append("</td><td>").Append(E(string.Join(", ", item.UnresolvedOutputFamilies))).Append("</td><td>").Append(E(string.Join(", ", item.SatisfiedGateIds))).Append("</td><td>").Append(E(terminalRoutes)).Append("</td><td>").Append(E(blockedRoutes)).Append("</td><td>").Append(E(string.Join(", ", item.RouteNames))).Append("</td><td>").Append(E(emitter + updates)).AppendLine("</td></tr>");
        }
        builder.AppendLine("</tbody></table></div></div></details>");
    }

    private static void AppendWorkflowDetails(StringBuilder builder, WorkflowInstance workflow)
    {
        builder.AppendLine("<details class=\"section\"><summary><span>Workflow nodes and transitions / 工作流节点与转移</span><span class=\"count\">" + workflow.GetStateNodes().Count + " states · " + workflow.GetTransitionNodes().Count + " transitions</span></summary><div class=\"section-body\">");
        foreach (var group in workflow.GetStateNodes().Values.GroupBy(static state => string.IsNullOrWhiteSpace(state.WorkflowPhase) ? "Not assigned / 未分配" : state.WorkflowPhase.Trim()).OrderBy(static group => group.Key, StringComparer.Ordinal))
        {
            builder.Append("<h3>").Append(E(group.Key)).AppendLine("</h3><div class=\"node-list\">");
            foreach (var state in group.OrderBy(static state => state.Id, StringComparer.Ordinal))
            {
                builder.Append("<article class=\"node\"><div><strong>").Append(E(state.Name)).Append("</strong> <code>").Append(E(state.Id)).Append("</code></div><p>").Append(E(state.Description ?? "Not provided / 未提供")).AppendLine("</p></article>");
                foreach (var transition in workflow.GetTransitionNodes().Values.Where(transition => state.Groups.Any(group => group.TransitionIds.Contains(transition.Id, StringComparer.Ordinal))).OrderBy(static transition => transition.Id, StringComparer.Ordinal))
                    builder.Append("<p class=\"transition\"><code>").Append(E(transition.Id)).Append("</code> · ").Append(E(transition.Name)).Append(" → <code>").Append(E(transition.TargetNodeId)).AppendLine("</code></p>");
            }
            builder.AppendLine("</div>");
        }
        builder.AppendLine("</div></details>");
    }

    private static void AppendSeams(StringBuilder builder, string title, IReadOnlyList<WorkflowSeamAnalysis> seams)
    {
        builder.Append("<h3>").Append(E(title)).AppendLine("</h3>");
        if (seams.Count == 0) builder.AppendLine("<p class=\"empty\">None recorded. / 未记录。</p>");
        foreach (var seam in seams)
            builder.Append("<p><code>").Append(E(seam.TransitionId)).Append("</code> · ").Append(E(seam.StepKind.ToString())).Append(" · owner: ").AppendLine(E(seam.OwnedInputMode ?? "not specified"));
    }

    private static void AppendStringList(StringBuilder builder, string title, IReadOnlyList<string> values)
    {
        if (!string.IsNullOrWhiteSpace(title)) builder.Append("<h4>").Append(E(title)).AppendLine("</h4>");
        if (values.Count == 0)
        {
            builder.AppendLine("<p class=\"empty\">None recorded. / 未记录。</p>");
            return;
        }
        builder.AppendLine("<ul>");
        foreach (var value in values) builder.Append("<li><code>").Append(E(value)).AppendLine("</code></li>");
        builder.AppendLine("</ul>");
    }

    private static void AddFact(StringBuilder builder, string label, string? value, string? cssClass = null)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        builder.Append("<div><dt>").Append(E(label)).Append("</dt><dd");
        if (!string.IsNullOrWhiteSpace(cssClass)) builder.Append(" class=\"").Append(cssClass).Append('"');
        builder.Append('>').Append(E(value)).AppendLine("</dd></div>");
    }

    private static void AddMetric(StringBuilder builder, string label, int value)
        => builder.Append("<div class=\"metric\"><span>").Append(E(label)).Append("</span><strong>").Append(value).AppendLine("</strong></div>");

    private static string E(object? value)
        => WebUtility.HtmlEncode(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty);

    private static readonly Regex HtmlTextNodePattern = new(@"(?<=>)(?<text>[^<>]*)(?=<)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex CjkTextPattern = new(@"[\u3400-\u4DBF\u4E00-\u9FFF]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static string MarkChineseText(string html)
        => HtmlTextNodePattern.Replace(html, match => CjkTextPattern.Replace(match.Groups["text"].Value, "<span lang=\"zh-CN\">$&</span>"));

    private const string Styles = """
        :root{color-scheme:light;--ink:#18232d;--muted:#52616d;--line:#d5dde2;--paper:#f4f6f5;--white:#fff;--green:#18744a;--red:#a52e32;--blue:#176b80;--amber:#8b5b00}
        *{box-sizing:border-box}body{margin:0;background:var(--paper);color:var(--ink);font:15px/1.5 'Segoe UI',Arial,sans-serif}.report{max-width:1440px;margin:0 auto;padding:28px clamp(16px,3vw,40px) 56px}.report-header{border-top:5px solid var(--blue);padding:20px 0 12px}.eyebrow{margin:0;color:var(--muted);font-size:12px;font-weight:700;letter-spacing:.08em}h1{font-size:30px;line-height:1.2;margin:8px 0 16px}h1 span{font-size:21px;color:var(--muted)}h2{font-size:20px}h3{font-size:16px;margin:22px 0 10px}h4{margin:14px 0 6px}.status{display:inline-flex;align-items:center;gap:9px;margin:0 0 20px;padding:7px 12px;background:var(--white);border:1px solid currentColor;font-weight:700}.status-pass{color:var(--green)}.status-fail{color:var(--red)}.status-mark{display:inline-grid;place-items:center;width:22px;height:22px;border:2px solid currentColor;border-radius:50%}.identity{display:grid;grid-template-columns:repeat(auto-fit,minmax(min(100%,280px),1fr));gap:1px;margin:0;background:var(--line);border:1px solid var(--line)}.identity>div{min-width:0;background:var(--white);padding:10px 12px}.identity dt{color:var(--muted);font-size:12px;font-weight:700}.identity dd{margin:3px 0 0;overflow-wrap:anywhere;font-weight:600}.identity .hash{font:12px/1.4 Consolas,monospace}.summary-grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(145px,1fr));gap:10px;margin:18px 0 22px}.metric{background:var(--white);border:1px solid var(--line);padding:13px 15px;display:flex;flex-direction:column;gap:4px}.metric span{color:var(--muted);font-size:13px}.metric strong{font-size:24px}.section{background:var(--white);border:1px solid var(--line);margin:12px 0}.section>summary{cursor:pointer;list-style:none;display:flex;justify-content:space-between;align-items:center;gap:16px;padding:14px 16px;font-size:17px;font-weight:700}.section>summary::-webkit-details-marker{display:none}.section>summary:before{content:'+';display:inline-grid;place-items:center;width:22px;height:22px;border:1px solid var(--line);margin-right:7px}.section[open]>summary:before{content:'−'}.section>summary span:first-child{margin-right:auto}.count{color:var(--muted);font-size:13px;font-weight:600;text-align:right}.section-body{padding:0 16px 18px}.table-wrap{overflow-x:auto;border:1px solid var(--line)}table{width:100%;border-collapse:collapse;font-size:13px}th,td{text-align:left;vertical-align:top;padding:9px 10px;border-bottom:1px solid var(--line)}th{background:#eaf0f1;color:#22343b;font-size:12px;position:sticky;top:0}tr:last-child td{border-bottom:0}td{overflow-wrap:anywhere}code{font:12px/1.45 Consolas,'Courier New',monospace;overflow-wrap:anywhere}.severity{display:inline-block;border:1px solid #7b4141;padding:2px 6px;font-weight:700;text-transform:uppercase}.suggestion{padding:8px 10px;background:#eef5f3;border-left:3px solid var(--green)}.muted,small,.empty{color:var(--muted)}.evidence-note{padding:10px 12px;border-left:3px solid var(--green);background:#edf5f1}.issues{padding:10px 12px 10px 34px;background:#fff2ef;border-left:3px solid var(--red)}.issues li{margin:5px 0}.node-list{border-left:2px solid var(--line);margin-left:5px;padding-left:14px}.node{border:1px solid var(--line);padding:10px 12px;margin:7px 0;background:#fbfcfc}.node strong{margin-right:8px}.node p{margin:5px 0 0;color:#33444e}.transition{margin:4px 0 8px 10px;padding-left:10px;border-left:2px solid #8ab4bd;color:#31454d;font-size:13px}footer{margin-top:20px;padding-top:12px;border-top:1px solid var(--line);font-size:12px;color:var(--muted)}@media(max-width:640px){.report{padding:16px 12px 36px}h1{font-size:25px}h1 span{display:block;font-size:18px}.section>summary{align-items:flex-start;font-size:15px;padding:12px}.section-body{padding:0 10px 14px}.summary-grid{grid-template-columns:repeat(2,minmax(0,1fr))}th,td{padding:8px;font-size:12px}}@media print{body{background:#fff}.report{max-width:none;padding:0}.section{break-inside:avoid}.section>summary:before{display:none}.section:not([open])>.section-body{display:block}.section>summary{cursor:default}}
        """;
}
