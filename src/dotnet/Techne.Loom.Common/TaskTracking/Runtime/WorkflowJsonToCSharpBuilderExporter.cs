using System.Text;
using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public static class WorkflowJsonToCSharpBuilderExporter
{
    public static string GenerateBuilderScript(string workflowJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowJson);
        return GenerateBuilderScript(WorkflowJsonSerializer.Deserialize(workflowJson));
    }

    public static string GenerateBuilderScript(WorkflowInstance workflow)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        EnsureTemplateOnly(workflow);

        var json = WorkflowJsonSerializer.Serialize(workflow);
        var delimiter = new string('"', Math.Max(3, LongestQuoteRun(json) + 1));
        var builder = new StringBuilder();
        builder.AppendLine("using Techne.Loom.Abstractions.TaskTracking.Model;");
        builder.AppendLine("using Techne.Loom.Common.TaskTracking.Runtime;");
        builder.AppendLine();
        builder.AppendLine("namespace Techne.Loom.WorkflowScripts;");
        builder.AppendLine();
        builder.AppendLine("public static class WorkflowInstanceBuilder");
        builder.AppendLine("{");
        builder.AppendLine("    public static WorkflowInstance Build(WorkflowScriptInput input)");
        builder.AppendLine("    {");
        builder.AppendLine("        // Keep the source workflow here and add focused C# edits below.");
        builder.AppendLine("        var workflow = WorkflowJsonSerializer.Deserialize(");
        builder.Append("            ").Append(delimiter).AppendLine();
        foreach (var line in json.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n'))
        {
            builder.Append("            ").AppendLine(line);
        }

        builder.Append("            ").Append(delimiter).AppendLine(");");
        builder.AppendLine("        workflow.RuntimeBinding = input.RuntimeBinding;");
        builder.AppendLine("        workflow.RuntimeVersion = input.RuntimeVersion;");
        builder.AppendLine("        return workflow;");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        return builder.ToString();
    }

    public static string GenerateBuilderTemplateMarkdown()
    {
        const string resourceName = "Techne.Loom.Common.WorkflowInstanceBuilderTemplate";
        using var stream = typeof(WorkflowJsonToCSharpBuilderExporter).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"The embedded workflow builder template resource '{resourceName}' is missing.");
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    private static void EnsureTemplateOnly(WorkflowInstance workflow)
    {
        if (workflow.Status is not (WorkflowStatus.Drafting or WorkflowStatus.ReadyToStart)
            || workflow.Version != 0
            || workflow.LastGateEvaluation is not null
            || workflow.History.Count != 0
            || workflow.ActiveWaitGroups.Count != 0
            || workflow.FanoutCancellationSignals.Count != 0
            || workflow.LastActivityUtc is not null
            || workflow.LastHeartbeatUtc is not null
            || workflow.LeaseOwner is not null
            || workflow.LeaseExpiresUtc is not null)
        {
            throw new InvalidOperationException("Only a draft or unstarted workflow template can be exported to a C# builder.");
        }
    }

    private static int LongestQuoteRun(string value)
    {
        var longest = 0;
        var current = 0;
        foreach (var character in value)
        {
            if (character == '"')
            {
                current++;
                longest = Math.Max(longest, current);
            }
            else
            {
                current = 0;
            }
        }

        return longest;
    }
}
