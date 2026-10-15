using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public sealed record ContextFieldProjectionDiagnostic(
    string Code,
    string Message,
    string Location,
    string SuggestedFix);

public static class ContextFieldProjectionValidator
{
    public static IReadOnlyList<ContextFieldProjectionDiagnostic> Validate(
        WorkflowInstance instance,
        ContextFieldCatalog catalog,
        string sourcePath)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(catalog);

        var diagnostics = new List<ContextFieldProjectionDiagnostic>();
        foreach (var transition in instance.GetTransitionNodes().Values.OfType<CommandTransition>())
        {
            if (!string.IsNullOrWhiteSpace(transition.OutputPath))
            {
                RequireTarget(
                    catalog,
                    transition.OutputPath,
                    $"transition:{transition.Id}/outputPath",
                    sourcePath,
                    diagnostics);
            }
            if (transition.Command.Parameters?.TryGetValue("updates", out var updatesValue) == true)
            {
                foreach (var update in GetMap(updatesValue))
                {
                    ValidateBindingValue(
                        catalog,
                        update.Key,
                        update.Value,
                        $"transition:{transition.Id}/updates/{update.Key}",
                        sourcePath,
                        diagnostics);
                }
            }

            if (transition.Command.Parameters?.TryGetValue("outputBindings", out var bindingsValue) == true)
            {
                foreach (var binding in GetMap(bindingsValue))
                {
                    ValidateBindingValue(
                        catalog,
                        binding.Key,
                        binding.Value,
                        $"transition:{transition.Id}/outputBindings/{binding.Key}",
                        sourcePath,
                        diagnostics);
                }
            }
        }

        return diagnostics;
    }

    private static void ValidateBindingValue(
        ContextFieldCatalog catalog,
        string targetPath,
        object? value,
        string location,
        string sourcePath,
        ICollection<ContextFieldProjectionDiagnostic> diagnostics)
    {
        if (!RequireTarget(catalog, targetPath, location, sourcePath, diagnostics))
        {
            return;
        }

        if (value is string marker
            && marker.StartsWith("$", StringComparison.Ordinal)
            && !string.Equals(marker, "$result", StringComparison.Ordinal)
            && !marker.StartsWith("$context:", StringComparison.Ordinal))
        {
            diagnostics.Add(new ContextFieldProjectionDiagnostic(
                "LOOM.CONTEXT.PROJECTION.SOURCE_UNSUPPORTED",
                $"Projection value '{marker}' uses an unsupported output binding expression.",
                location,
                "Use '$result', a '$context:<path>' reference, or a literal JSON value."));
            return;
        }

        if (value is string text && string.Equals(text, "$result", StringComparison.Ordinal))
        {
            return;
        }

        if (value is string contextReference && contextReference.StartsWith("$context:", StringComparison.Ordinal))
        {
            var sourceContextPath = contextReference["$context:".Length..];
            if (!catalog.TryGetField(sourceContextPath, out var sourceField))
            {
                diagnostics.Add(new ContextFieldProjectionDiagnostic(
                    "LOOM.CONTEXT.PROJECTION.SOURCE_UNDECLARED",
                    $"Projection source '{sourceContextPath}' is not declared by the owning context_fields catalog.",
                    location,
                    "Declare the source path and use a destination field with the same JSON type."));
                return;
            }

            catalog.TryGetField(targetPath, out var targetField);
            if (!HasCompatibleType(sourceField, targetField))
            {
                diagnostics.Add(new ContextFieldProjectionDiagnostic(
                    "LOOM.CONTEXT.PROJECTION.TYPE_MISMATCH",
                    $"Projection from '{sourceContextPath}' ({Describe(sourceField)}) to '{targetPath}' ({Describe(targetField)}) is not type-compatible.",
                    location,
                    "Declare source and destination with the same JSON type and container shape; a nullable source cannot target a non-nullable field."));
            }

            return;
        }

        try
        {
            catalog.ValidateFieldValue(targetPath, value, sourcePath);
        }
        catch (InvalidOperationException exception)
        {
            diagnostics.Add(new ContextFieldProjectionDiagnostic(
                "LOOM.CONTEXT.PROJECTION.VALUE_MISMATCH",
                exception.Message,
                location,
                "Change the projected literal to match the destination context_fields type or correct the owning declaration."));
        }
    }

    private static bool RequireTarget(
        ContextFieldCatalog catalog,
        string path,
        string location,
        string sourcePath,
        ICollection<ContextFieldProjectionDiagnostic> diagnostics)
    {
        if (catalog.TryGetField(path, out _))
        {
            return true;
        }

        diagnostics.Add(new ContextFieldProjectionDiagnostic(
            "LOOM.CONTEXT.PROJECTION.TARGET_UNDECLARED",
            $"Projection destination '{path}' is not declared by the owning context_fields catalog '{sourcePath}'.",
            location,
            "Declare the destination context path with its JSON type in the owning context_fields catalog."));
        return false;
    }

    private static bool HasCompatibleType(ContextFieldDefinition source, ContextFieldDefinition target)
        => string.Equals(source.Type, target.Type, StringComparison.Ordinal)
            && string.Equals(source.ItemType, target.ItemType, StringComparison.Ordinal)
            && string.Equals(source.ValueType, target.ValueType, StringComparison.Ordinal)
            && (!source.Nullable || target.Nullable);

    private static string Describe(ContextFieldDefinition field)
        => field.Type switch
        {
            "array" => $"{field.Type}<{field.ItemType}>",
            "object" when field.ValueType is not null => $"{field.Type}<string,{field.ValueType}>",
            _ => field.Type,
        } + (field.Nullable ? "?" : string.Empty);

    private static IEnumerable<KeyValuePair<string, object?>> GetMap(object? value)
        => value switch
        {
            IDictionary<string, object?> mutable => mutable,
            IReadOnlyDictionary<string, object?> readOnly => readOnly,
            JsonElement { ValueKind: JsonValueKind.Object } element => element.EnumerateObject()
                .Select(static property => new KeyValuePair<string, object?>(property.Name, property.Value.Clone())),
            _ => [],
        };
}
