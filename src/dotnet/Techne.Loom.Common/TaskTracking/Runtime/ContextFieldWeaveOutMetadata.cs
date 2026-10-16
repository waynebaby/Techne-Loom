using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public sealed record ContextFieldConsumerExpression(
    [property: JsonPropertyName("location")] string Location,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("read_as")] string ReadAs);

public sealed record ContextFieldWeaveOutField(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("nullable")] bool Nullable,
    [property: JsonPropertyName("owner")] string Owner,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("item_type")] string? ItemType,
    [property: JsonPropertyName("value_type")] string? ValueType,
    [property: JsonPropertyName("minimum")] decimal? Minimum,
    [property: JsonPropertyName("maximum")] decimal? Maximum,
    [property: JsonPropertyName("allowed_values")] IReadOnlyList<JsonElement> AllowedValues,
    [property: JsonPropertyName("suggested_values")] IReadOnlyList<ContextFieldSuggestion> SuggestedValues,
    [property: JsonPropertyName("suggestion_reason")] string? SuggestionReason,
    [property: JsonPropertyName("consumer_expressions")] IReadOnlyList<ContextFieldConsumerExpression> ConsumerExpressions,
    [property: JsonPropertyName("host_decision_guidance")] string? HostDecisionGuidance);

public static class ContextFieldWeaveOutMetadataBuilder
{
    public static IReadOnlyList<ContextFieldWeaveOutField> Build(
        WorkflowInstance instance,
        ContextFieldCatalog catalog,
        TransitionBase? pendingTransition)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(catalog);
        if (pendingTransition is null)
        {
            return [];
        }

        var pendingTransitions = GetBoundaryTransitions(instance, pendingTransition);
        var consumers = GetConsumerExpressions(instance, pendingTransitions);
        var relevantPaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var transition in pendingTransitions)
        {
            relevantPaths.UnionWith(GetRequiredInputs(transition));
            if (transition is not CommandTransition commandTransition)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(commandTransition.OutputPath))
            {
                relevantPaths.Add(commandTransition.OutputPath);
            }

            if (commandTransition.Command.Parameters?.TryGetValue("outputBindings", out var outputBindings) == true)
            {
                relevantPaths.UnionWith(GetMap(outputBindings).Select(static binding => binding.Key));
            }
        }

        relevantPaths.UnionWith(consumers.Keys);
        return relevantPaths
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static path => path, StringComparer.Ordinal)
            .Where(path => catalog.TryGetField(path, out _))
            .Select(path =>
            {
                catalog.TryGetField(path, out var field);
                return new ContextFieldWeaveOutField(
                    field.Path,
                    field.Type,
                    field.Nullable,
                    field.Owner,
                    field.Description,
                    field.ItemType,
                    field.ValueType,
                    field.Minimum,
                    field.Maximum,
                    field.AllowedValues,
                    field.SuggestedValues,
                    field.SuggestionReason,
                    consumers.TryGetValue(path, out var expressions) ? expressions : [],
                    field.Owner == "user"
                        ? "Collect this decision from the user; do not infer or override it."
                        : null);
            })
            .ToArray();
    }

    private static IReadOnlyList<TransitionBase> GetBoundaryTransitions(WorkflowInstance instance, TransitionBase pendingTransition)
    {
        var transitionIds = new HashSet<string>(StringComparer.Ordinal) { pendingTransition.Id };
        foreach (var waitGroup in instance.ActiveWaitGroups.Where(group =>
            string.Equals(group.TransitionId, pendingTransition.Id, StringComparison.Ordinal)
            || group.ExpectedTransitionIds.Contains(pendingTransition.Id, StringComparer.Ordinal)))
        {
            transitionIds.UnionWith(waitGroup.ExpectedTransitionIds);
        }

        return transitionIds
            .Where(instance.Nodes.ContainsKey)
            .Select(id => instance.Nodes[id])
            .OfType<TransitionBase>()
            .ToArray();
    }

    private static HashSet<string> GetRequiredInputs(TransitionBase transition)
    {
        var inputs = new HashSet<string>(StringComparer.Ordinal);
        if (transition is not CommandTransition commandTransition)
        {
            return inputs;
        }

        if (commandTransition.Plan is not null)
        {
            inputs.UnionWith(commandTransition.Plan.InputPaths);
        }

        if (commandTransition.Command.Parameters?.TryGetValue("requiredInputs", out var value) == true)
        {
            inputs.UnionWith(GetStringValues(value));
        }

        return inputs;
    }

    private static Dictionary<string, IReadOnlyList<ContextFieldConsumerExpression>> GetConsumerExpressions(
        WorkflowInstance instance,
        IReadOnlyList<TransitionBase> pendingTransitions)
    {
        var selectedTransitions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pendingTransition in pendingTransitions)
        {
            selectedTransitions.Add(pendingTransition.Id);
            if (string.IsNullOrWhiteSpace(pendingTransition.TargetNodeId)
                || !instance.Nodes.TryGetValue(pendingTransition.TargetNodeId, out var targetNode)
                || targetNode is not StateNode targetState)
            {
                continue;
            }

            selectedTransitions.UnionWith(targetState.Groups.SelectMany(static group => group.TransitionIds));
        }

        var consumers = new Dictionary<string, List<ContextFieldConsumerExpression>>(StringComparer.Ordinal);
        foreach (var transitionId in selectedTransitions)
        {
            if (!instance.Nodes.TryGetValue(transitionId, out var node) || node is not TransitionBase transition)
            {
                continue;
            }

            AddExpressionConsumers(consumers, $"transition:{transitionId}/guardExpression", transition.GuardExpression.Source);
            AddExpressionConsumers(consumers, $"transition:{transitionId}/succeedExpression", transition.SucceedExpression.Source);

            var gateIds = new HashSet<string>(transition.SatisfiesGateIds ?? [], StringComparer.Ordinal);
            if (transition is CommandTransition commandTransition)
            {
                if (commandTransition.Command.Parameters?.TryGetValue("satisfiesGateIds", out var gateIdsValue) == true)
                {
                    gateIds.UnionWith(GetStringValues(gateIdsValue));
                }

                if (commandTransition.Command.Parameters?.TryGetValue("outputBindings", out var outputBindings) == true)
                {
                    foreach (var binding in GetMap(outputBindings))
                    {
                        if (binding.Value is string source)
                        {
                            var sourcePrefix = source.StartsWith("$context:", StringComparison.Ordinal)
                                ? "$context:"
                                : source.StartsWith("$payload:", StringComparison.Ordinal) ? "$payload:" : null;
                            if (sourcePrefix is not null)
                            {
                                AddConsumer(
                                    consumers,
                                    source[sourcePrefix.Length..],
                                    new ContextFieldConsumerExpression(
                                        $"transition:{transitionId}/outputBindings/{binding.Key}",
                                        source,
                                        "projection"));
                            }
                        }
                    }
                }
            }

            if (instance.Validation is not null)
            {
                foreach (var gateId in gateIds)
                {
                    if (instance.Validation.Gates.TryGetValue(gateId, out var gate)
                        && gate.PassExpression is { } passExpression)
                    {
                        AddExpressionConsumers(consumers, $"validation.gates.{gateId}/passExpression", passExpression.Source);
                    }
                }
            }
        }

        return consumers.ToDictionary(
            static pair => pair.Key,
            static pair => (IReadOnlyList<ContextFieldConsumerExpression>)pair.Value,
            StringComparer.Ordinal);
    }


    private static bool TryGetContextConsumerPath(ExpressionSyntax expression, out string path)
    {
        if (expression is LiteralExpressionSyntax literal
            && literal.Kind() == Microsoft.CodeAnalysis.CSharp.SyntaxKind.StringLiteralExpression)
        {
            path = literal.Token.ValueText;
            return true;
        }

        if (expression is BinaryExpressionSyntax binary
            && binary.RawKind == (int)SyntaxKind.AddExpression
            && TryGetConstantString(binary.Left, out var binaryPrefix)
            && binaryPrefix.EndsWith(".", StringComparison.Ordinal))
        {
            path = binaryPrefix[..^1];
            return true;
        }

        if (expression is InterpolatedStringExpressionSyntax interpolated
            && interpolated.Contents.OfType<InterpolationSyntax>().FirstOrDefault() is { } firstInterpolation)
        {
            var prefix = string.Concat(interpolated.Contents
                .OfType<InterpolatedStringTextSyntax>()
                .TakeWhile(item => item.SpanStart < firstInterpolation.SpanStart)
                .Select(static item => item.TextToken.ValueText));
            if (prefix.EndsWith(".", StringComparison.Ordinal))
            {
                path = prefix[..^1];
                return true;
            }
        }

        path = string.Empty;
        return false;
    }

    private static bool TryGetConstantString(ExpressionSyntax expression, out string value)
    {
        if (expression is LiteralExpressionSyntax literal
            && literal.Kind() == Microsoft.CodeAnalysis.CSharp.SyntaxKind.StringLiteralExpression)
        {
            value = literal.Token.ValueText;
            return true;
        }

        if (expression is BinaryExpressionSyntax binary
            && binary.RawKind == (int)SyntaxKind.AddExpression
            && TryGetConstantString(binary.Left, out var left)
            && TryGetConstantString(binary.Right, out var right))
        {
            value = left + right;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static void AddExpressionConsumers(
        IDictionary<string, List<ContextFieldConsumerExpression>> consumers,
        string location,
        string source)
    {
        var expression = SyntaxFactory.ParseExpression(source);
        foreach (var invocation in expression.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax member)
            {
                continue;
            }

            var methodName = member.Name.Identifier.ValueText;
            if (methodName is not ("Get" or "Has") || invocation.ArgumentList.Arguments.Count != 1)
            {
                continue;
            }

            if (!TryGetContextConsumerPath(invocation.ArgumentList.Arguments[0].Expression, out var path))
            {
                continue;
            }
            var readAs = methodName == "Has"
                ? "presence"
                : member.Name is GenericNameSyntax genericName && genericName.TypeArgumentList.Arguments.Count == 1
                    ? genericName.TypeArgumentList.Arguments[0].ToString()
                    : "unknown";
            AddConsumer(consumers, path, new ContextFieldConsumerExpression(location, source, readAs));
        }
    }

    private static void AddConsumer(
        IDictionary<string, List<ContextFieldConsumerExpression>> consumers,
        string path,
        ContextFieldConsumerExpression consumer)
    {
        if (!consumers.TryGetValue(path, out var expressions))
        {
            expressions = [];
            consumers[path] = expressions;
        }

        expressions.Add(consumer);
    }


    private static IEnumerable<KeyValuePair<string, object?>> GetMap(object? value)
    {
        return value switch
        {
            IDictionary<string, object?> mutable => mutable,
            IReadOnlyDictionary<string, object?> readOnly => readOnly,
            JsonElement { ValueKind: JsonValueKind.Object } element => element.EnumerateObject()
                .Select(static property => new KeyValuePair<string, object?>(property.Name, property.Value.Clone())),
            _ => [],
        };
    }

    private static IEnumerable<string> GetStringValues(object? value)
    {
        return value switch
        {
            IEnumerable<object?> values => values.Select(Convert.ToString).Where(static item => !string.IsNullOrWhiteSpace(item)).Cast<string>(),
            JsonElement { ValueKind: JsonValueKind.Array } array => array.EnumerateArray()
                .Where(static item => item.ValueKind == JsonValueKind.String)
                .Select(static item => item.GetString()!)
                .Where(static item => !string.IsNullOrWhiteSpace(item)),
            _ => [],
        };
    }
}
