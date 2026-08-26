using System.Text;
using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public static class WorkflowModelReferenceExporter
{
    public static string Generate(WorkflowSchemaContract schema)
    {
        ArgumentNullException.ThrowIfNull(schema);

        var builder = new StringBuilder();
        builder.AppendLine("using System;");
        builder.AppendLine("using System.Collections.Generic;");
        builder.AppendLine("using Techne.Loom.Abstractions.TaskTracking.Model;");
        builder.AppendLine();
        builder.AppendLine("public static class WorkflowModelReferenceFacade");
        builder.AppendLine("{");
        AppendConstant(builder, "SchemaId", schema.SchemaId);
        AppendConstant(builder, "SchemaVersion", schema.SchemaVersion);
        AppendConstant(builder, "RuntimeType", schema.RuntimeType);
        AppendConstant(builder, "NodeKindDiscriminator", schema.NodeKindDiscriminator);
        builder.AppendLine();
        builder.AppendLine("    public static WorkflowInstance NewWorkflowInstance() => new();");
        builder.AppendLine("    public static StateNode NewStateNode() => new();");
        builder.AppendLine("    public static CommandTransition NewCommandTransition() => new();");
        builder.AppendLine("    public static ExpressionTransition NewExpressionTransition() => new();");
        builder.AppendLine("    public static ToBeRefinedTransition NewToBeRefinedTransition() => new();");
        builder.AppendLine("    public static TransitionGroup NewTransitionGroup() => new();");
        builder.AppendLine("    public static CommandInvocation NewCommandInvocation() => new();");
        builder.AppendLine("    public static ExpressionDefinition Predicate(string source) => new() { Kind = \"predicate\", Source = source, ResultType = \"bool\" };");
        builder.AppendLine("    public static WorkflowScriptVerificationSuite NewVerificationSuite() => new();");
        builder.AppendLine();
        AppendStringArray(builder, "RootFields", schema.RootFields);
        AppendStringArray(builder, "RequiredRootFields", schema.RequiredRootFields);
        AppendStringArray(builder, "ExpressionDefinitionFields", schema.ExpressionDefinitionFields);
        AppendDictionary(builder, "NodeFields", schema.NodeFields);
        AppendDictionary(builder, "RequiredNodeFields", schema.RequiredNodeFields);
        AppendDictionary(builder, "AllowedValues", schema.AllowedValues);
        AppendStringDictionary(builder, "CommandParameterContracts", schema.CommandParameterContracts);
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static void AppendConstant(StringBuilder builder, string name, string value)
    {
        builder.Append("    public const string ");
        builder.Append(name);
        builder.Append(" = ");
        builder.Append(ToLiteral(value));
        builder.AppendLine(";");
    }

    private static void AppendStringArray(StringBuilder builder, string name, IReadOnlyList<string> values)
    {
        builder.Append("    public static IReadOnlyList<string> ");
        builder.Append(name);
        builder.Append(" { get; } = new[] { ");
        builder.Append(string.Join(", ", values.Select(ToLiteral)));
        builder.AppendLine(" };");
    }

    private static void AppendStringDictionary(

        StringBuilder builder,

        string name,

        IReadOnlyDictionary<string, string> values)

    {

        builder.AppendLine($"    public static IReadOnlyDictionary<string, string> {name} {{ get; }} = new Dictionary<string, string>(StringComparer.Ordinal)");

        builder.AppendLine("    {");

        foreach (var pair in values.OrderBy(static pair => pair.Key, StringComparer.Ordinal))

        {

            builder.Append("        [");

            builder.Append(ToLiteral(pair.Key));

            builder.Append("] = ");

            builder.Append(ToLiteral(pair.Value));

            builder.AppendLine(",");

        }



        builder.AppendLine("    };");

    }

    private static void AppendDictionary(
        StringBuilder builder,
        string name,
        IReadOnlyDictionary<string, IReadOnlyList<string>> values)
    {
        builder.AppendLine($"    public static IReadOnlyDictionary<string, IReadOnlyList<string>> {name} {{ get; }} = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)");
        builder.AppendLine("    {");
        foreach (var pair in values.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            builder.Append("        [");
            builder.Append(ToLiteral(pair.Key));
            builder.Append("] = new[] { ");
            builder.Append(string.Join(", ", pair.Value.Select(ToLiteral)));
            builder.AppendLine(" },");
        }

        builder.AppendLine("    };");
    }

    private static string ToLiteral(string value)
    {
        var escaped = value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
        return $"\"{escaped}\"";
    }
}
