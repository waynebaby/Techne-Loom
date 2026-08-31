using System;
using System.Collections.Generic;

namespace Techne.Loom.Abstractions.TaskTracking.Model;

public static class WorkflowIdentityContract
{
    public const string SkillEnhancementTaskType = "skill_enhancement";
    public const string SoSelfBootstrapWorkflowKind = "so_self_bootstrap";
    public const string TargetSkillEnhancementWorkflowKind = "target_skill_enhancement";
    public const string TargetSkillBusinessWorkflowKind = "target_skill_business";
    public const string TemplateRunIdPrefix = "template:";

    private static readonly HashSet<string> EnhancementOutputFamilies = new(StringComparer.Ordinal)
    {
        "shared_review_context",
        "parallel_review_batch_evidence",
        "reenhancement_skill_markdown_gap_review",
        "reenhancement_package_lock_gap_review",
        "reenhancement_workflow_gap_review",
        "reenhancement_template_strategy_review",
        "reenhancement_template_change_strategy",
        "reenhancement_template_change_evidence",
        "aggregated_reenhancement_findings",
        "aggregated_plan_findings",
        "aggregated_review_findings",
        "batch_repair_evidence",
        "parallel_post_fix_validation_evidence",
        "aggregated_post_fix_validation",
        "serial_validation_evidence",
        "review_fix_loop_evidence",
    };

    public static bool IsKnownWorkflowKind(string? workflowKind)
        => workflowKind is SoSelfBootstrapWorkflowKind or TargetSkillEnhancementWorkflowKind or TargetSkillBusinessWorkflowKind;

    public static bool IsEnhancementWorkflowKind(string? workflowKind)
        => workflowKind is SoSelfBootstrapWorkflowKind or TargetSkillEnhancementWorkflowKind;

    public static bool IsTargetSkillBusinessWorkflowKind(string? workflowKind)
        => string.Equals(workflowKind, TargetSkillBusinessWorkflowKind, StringComparison.Ordinal);

    public static bool IsEnhancementOutputFamily(string? outputFamily)
        => !string.IsNullOrWhiteSpace(outputFamily) && EnhancementOutputFamilies.Contains(outputFamily);

    public static bool IsEnhancementSubagentPath(string? path)
        => !string.IsNullOrWhiteSpace(path)
            && path.StartsWith("assets/agents/loom-skill-enhancement-", StringComparison.Ordinal);

    public static bool IsTemplateRunId(string? runId)
        => !string.IsNullOrWhiteSpace(runId)
            && runId.StartsWith(TemplateRunIdPrefix, StringComparison.Ordinal);

    public static string CreateRuntimeRunId()
        => $"run-{Guid.NewGuid():N}";
}
