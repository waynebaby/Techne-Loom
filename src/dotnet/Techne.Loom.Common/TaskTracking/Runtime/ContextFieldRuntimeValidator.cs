using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public sealed record ContextFieldResumeAdmissionResult(
    Dictionary<string, object?>? Payload,
    IReadOnlyList<string> Warnings);

public static class ContextFieldRuntimeValidator
{
    public static void ValidateWorkflowDefinition(
        WorkflowInstance instance,
        ContextFieldCatalog catalog,
        string sourcePath)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(catalog);

        var projectionDiagnostics = ContextFieldProjectionValidator.Validate(instance, catalog, sourcePath);
        if (projectionDiagnostics.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, projectionDiagnostics.Select(static diagnostic => diagnostic.Message)));
        }

        var compiler = new ExpressionCompilerRouter();
        foreach (var transition in instance.GetTransitionNodes().Values.OrderBy(static transition => transition.Id, StringComparer.Ordinal))
        {
            ValidateExpression(compiler, instance.ExpressionBinding, catalog, transition.GuardExpression, $"transition:{transition.Id}/guardExpression", sourcePath);
            ValidateExpression(compiler, instance.ExpressionBinding, catalog, transition.SucceedExpression, $"transition:{transition.Id}/succeedExpression", sourcePath);
        }

        if (instance.Validation is not null)
        {
            foreach (var gate in instance.Validation.Gates.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
            {
                if (gate.Value.PassExpression is { } passExpression)
                {
                    ValidateExpression(compiler, instance.ExpressionBinding, catalog, passExpression, $"validation.gates.{gate.Key}/passExpression", sourcePath);
                }
            }
        }
    }

    private static void ValidateExpression(
        ExpressionCompilerRouter compiler,
        ExpressionBinding binding,
        ContextFieldCatalog catalog,
        ExpressionDefinition expression,
        string field,
        string sourcePath)
    {
        var result = compiler.CompileWithContextFieldCatalog(binding, expression, catalog, field);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"Workflow expression '{field}' is invalid for the owning context_fields catalog '{sourcePath}': {result.Feedback.Message}");
        }
    }

    public static void ValidateContextDelta(
        ContextFieldCatalog catalog,
        IReadOnlyDictionary<string, object?> context,
        Dictionary<string, object?>? contextDelta,
        string sourcePath)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(context);
        if (contextDelta is null)
        {
            return;
        }

        foreach (var key in contextDelta.Keys)
        {
            if (!IsDeclaredContextPathOrContainer(catalog, key))
            {
                throw new InvalidOperationException($"Context update '{key}' is not declared by context_fields.");
            }

            foreach (var field in catalog.Fields.Values.Where(static field => field.Owner == "runtime"))
            {
                if (string.Equals(key, field.Path, StringComparison.Ordinal)
                    || key.StartsWith(field.Path + ".", StringComparison.Ordinal)
                    || field.Path.StartsWith(key + ".", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"Context update '{key}' attempts to write runtime-owned field '{field.Path}'.");
                }
            }
        }

        var candidateContext = new Dictionary<string, object?>(context, StringComparer.Ordinal);
        foreach (var pair in contextDelta)
        {
            candidateContext[pair.Key] = pair.Value;
        }

        catalog.ValidateContextValues(candidateContext, sourcePath);
    }

    private static bool IsDeclaredContextPathOrContainer(ContextFieldCatalog catalog, string path)
    {
        if (catalog.TryGetField(path, out _))
        {
            return true;
        }

        var descendantPrefix = path + ".";
        if (catalog.Fields.Keys.Any(fieldPath => fieldPath.StartsWith(descendantPrefix, StringComparison.Ordinal)))
        {
            return true;
        }

        var separator = path.LastIndexOf('.');
        if (separator <= 0
            || !catalog.TryGetField(path[..separator], out var parent)
            || parent.Type != "object"
            || parent.ValueType is null)
        {
            return false;
        }

        return !path[(separator + 1)..].Contains('.', StringComparison.Ordinal);
    }

    public static ContextFieldResumeAdmissionResult ValidateResumeProjection(
        WorkflowInstance instance,
        ContextFieldCatalog catalog,
        string transitionId,
        Dictionary<string, object?>? payload,
        string sourcePath,
        string? executionError = null)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(catalog);

        var allowedRoots = catalog.Fields.Keys
            .Select(GetRootPath)
            .ToHashSet(StringComparer.Ordinal);
        CommandTransition? pendingTransition = instance.Nodes.TryGetValue(transitionId, out var pendingNode)
            ? pendingNode as CommandTransition
            : null;
        var outputKey = pendingTransition?.Command.Parameters?.TryGetValue("resumeOutputKey", out var outputKeyValue) == true
            ? Convert.ToString(outputKeyValue)
            : null;
        if (!string.IsNullOrWhiteSpace(outputKey))
        {
            allowedRoots.Add(GetRootPath(outputKey));
        }

        Dictionary<string, object?>? admittedPayload = null;
        var warnings = new List<string>();
        if (payload is not null)
        {
            admittedPayload = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var pair in payload)
            {
                if (allowedRoots.Contains(pair.Key) || catalog.TryGetField(pair.Key, out _))
                {
                    admittedPayload[pair.Key] = pair.Value;
                }
                else
                {
                    warnings.Add($"Filtered undeclared resume payload field '{pair.Key}'.");
                }
            }

            var candidateContext = new Dictionary<string, object?>(instance.Context, StringComparer.Ordinal);
            foreach (var pair in admittedPayload)
            {
                candidateContext[pair.Key] = pair.Value;
            }

            catalog.ValidateContextValues(candidateContext, sourcePath, allowedRoots);
            var allowsRuntimeOwnedOutputs = pendingTransition?.StepKind == WorkflowStepKind.WaitResume;
            var declaredTransitionOutputs = new HashSet<string>(StringComparer.Ordinal);
            if (pendingTransition is not null)
            {
                if (!string.IsNullOrWhiteSpace(pendingTransition.OutputPath))
                {
                    declaredTransitionOutputs.Add(pendingTransition.OutputPath);
                    if (!allowsRuntimeOwnedOutputs)
                    {
                        ValidateRuntimeOwnedOutputPath(catalog, pendingTransition.OutputPath, transitionId);
                    }
                }
                if (!string.IsNullOrWhiteSpace(outputKey))
                {
                    declaredTransitionOutputs.Add(outputKey);
                }
                if (pendingTransition.Command.Parameters?.TryGetValue("outputBindings", out var outputBindingsValue) == true
                    && outputBindingsValue is IDictionary<string, object?> outputBindings)
                {
                    foreach (var outputBinding in outputBindings)
                    {
                        declaredTransitionOutputs.Add(outputBinding.Key);
                        var contextSourcePath = outputBinding.Value is string source
                            && source.StartsWith("$context:", StringComparison.Ordinal)
                            ? source[9..]
                            : null;
                        var isTrustedContextPassThrough = contextSourcePath is not null
                            && !IsContextPathFromExternalResult(pendingTransition, contextSourcePath);
                        if (!allowsRuntimeOwnedOutputs && !isTrustedContextPassThrough)
                        {
                            ValidateRuntimeOwnedOutputPath(catalog, outputBinding.Key, transitionId);
                        }
                    }
                }
            }
            foreach (var pair in admittedPayload)
            {
                foreach (var field in catalog.Fields.Values.Where(static field => field.Owner == "runtime"))
                {
                    if (allowsRuntimeOwnedOutputs && declaredTransitionOutputs.Any(outputPath =>
                            string.Equals(pair.Key, outputPath, StringComparison.Ordinal)
                            || pair.Key.StartsWith(outputPath + ".", StringComparison.Ordinal)))
                    {
                        continue;
                    }

                    if (PayloadContainsContextPath(pair.Key, pair.Value, field.Path))
                    {
                        throw new InvalidOperationException($"Resume payload cannot write runtime-owned context field '{field.Path}'.");
                    }
                }

                if (catalog.TryGetField(pair.Key, out _))
                {
                    catalog.ValidateFieldValue(pair.Key, pair.Value, sourcePath);
                }
            }
        }

        if (pendingTransition is null || executionError is not null)
        {
            return new ContextFieldResumeAdmissionResult(admittedPayload, warnings);
        }

        object? output = admittedPayload;
        if (!string.IsNullOrWhiteSpace(outputKey))
        {
            if (admittedPayload is null || !PathValueAccessor.TryGetValue(admittedPayload, outputKey, out output))
            {
                throw new InvalidOperationException($"Resume payload is missing declared resumeOutputKey '{outputKey}'.");
            }
        }
        if (!string.IsNullOrWhiteSpace(pendingTransition.OutputPath))
        {
            catalog.ValidateFieldValue(pendingTransition.OutputPath, output, sourcePath);
        }

        if (pendingTransition.Command.Parameters?.TryGetValue("outputBindings", out var bindingsValue) == true
            && bindingsValue is IDictionary<string, object?> bindings)
        {
            foreach (var binding in bindings)
            {
                object? value = binding.Value switch
                {
                    string text when string.Equals(text, "$result", StringComparison.Ordinal) => output,
                    string text when text.StartsWith("$context:", StringComparison.Ordinal) => GetResumeProjectionValue(instance, pendingTransition, output, text[9..]),
                    string text when text.StartsWith("$payload:", StringComparison.Ordinal) => GetResumePayloadValue(admittedPayload, text[9..]),
                    _ => binding.Value,
                };
                catalog.ValidateFieldValue(binding.Key, value, sourcePath);
            }
        }

        return new ContextFieldResumeAdmissionResult(admittedPayload, warnings);
    }

    private static bool PayloadContainsContextPath(string payloadPath, object? payloadValue, string contextPath)
    {
        if (string.Equals(payloadPath, contextPath, StringComparison.Ordinal)
            || payloadPath.StartsWith(contextPath + ".", StringComparison.Ordinal))
        {
            return true;
        }

        var parentPrefix = payloadPath + ".";
        if (!contextPath.StartsWith(parentPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var relativePath = contextPath[parentPrefix.Length..];
        var wrappedValue = new Dictionary<string, object?>(StringComparer.Ordinal) { ["payload"] = payloadValue };
        return PathValueAccessor.TryGetValue(wrappedValue, "payload." + relativePath, out _);
    }

    private static object? GetResumePayloadValue(IReadOnlyDictionary<string, object?>? payload, string payloadPath)
    {
        if (payload is null
            || string.IsNullOrWhiteSpace(payloadPath)
            || !PathValueAccessor.TryGetValue(payload, payloadPath, out var value))
        {
            throw new InvalidOperationException($"Resume projection source '{payloadPath}' is missing from the submitted result.");
        }

        return value;
    }

    private static bool IsContextPathFromExternalResult(CommandTransition transition, string contextPath)
    {
        var outputPath = transition.OutputPath;
        return !string.IsNullOrWhiteSpace(outputPath)
            && (string.Equals(contextPath, outputPath, StringComparison.Ordinal)
                || contextPath.StartsWith(outputPath + ".", StringComparison.Ordinal));
    }

    private static object? GetResumeProjectionValue(
        WorkflowInstance instance,
        CommandTransition transition,
        object? output,
        string contextPath)
    {
        var outputPath = transition.OutputPath;
        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            if (string.Equals(contextPath, outputPath, StringComparison.Ordinal))
            {
                return output;
            }

            var outputPrefix = outputPath + ".";
            if (contextPath.StartsWith(outputPrefix, StringComparison.Ordinal))
            {
                var outputContext = new Dictionary<string, object?>(StringComparer.Ordinal) { ["output"] = output };
                if (!PathValueAccessor.TryGetValue(outputContext, "output." + contextPath[outputPrefix.Length..], out var nestedValue))
                {
                    throw new InvalidOperationException($"Resume projection source '{contextPath}' is missing from the submitted result.");
                }

                return nestedValue;
            }
        }

        if (!PathValueAccessor.TryGetValue(instance.Context, contextPath, out var contextValue))
        {
            throw new InvalidOperationException($"Resume projection source '{contextPath}' is missing from the existing workflow context.");
        }

        return contextValue;
    }
    private static void ValidateRuntimeOwnedOutputPath(ContextFieldCatalog catalog, string outputPath, string transitionId)
    {
        if (catalog.TryGetField(outputPath, out var targetField))
        {
            if (targetField.Owner == "runtime"
                || catalog.Fields.Values.Any(field => field.Owner == "runtime"
                    && field.Path.StartsWith(outputPath + ".", StringComparison.Ordinal)))
            {
                var runtimeFieldPath = targetField.Owner == "runtime"
                    ? targetField.Path
                    : catalog.Fields.Values.First(field => field.Owner == "runtime"
                        && field.Path.StartsWith(outputPath + ".", StringComparison.Ordinal)).Path;
                throw new InvalidOperationException(
                    $"External transition '{transitionId}' cannot project runtime-owned context field '{runtimeFieldPath}' outside a WaitResume boundary.");
            }

            return;
        }

        foreach (var field in catalog.Fields.Values.Where(static field => field.Owner == "runtime"))
        {
            if (outputPath.StartsWith(field.Path + ".", StringComparison.Ordinal)
                || field.Path.StartsWith(outputPath + ".", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"External transition '{transitionId}' cannot project runtime-owned context field '{field.Path}' outside a WaitResume boundary.");
            }
        }
    }
    private static string GetRootPath(string path)
    {
        var separator = path.IndexOf('.', StringComparison.Ordinal);
        return separator < 0 ? path : path[..separator];
    }
}
