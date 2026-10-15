namespace Techne.Loom.Common.TaskTracking.Runtime;

public static class PathValueAccessor
{
    public static object? GetValue(IReadOnlyDictionary<string, object?> context, string? path)
    {
        return TryGetValue(context, path, out var value) && value is not System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.Null or System.Text.Json.JsonValueKind.Undefined }
            ? value
            : null;
    }

    public static bool TryGetValue(IReadOnlyDictionary<string, object?> context, string? path, out object? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (context.TryGetValue(path, out value))
        {
            return true;
        }

        return TryGetNestedValue(context, path, out value);
    }

    public static bool TryGetNestedValue(IReadOnlyDictionary<string, object?> context, string? path, out object? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length < 2)
        {
            return false;
        }

        object? current = context;
        foreach (var segment in segments)
        {
            switch (current)
            {
                case IReadOnlyDictionary<string, object?> readOnly when readOnly.TryGetValue(segment, out var readOnlyValue):
                    current = readOnlyValue;
                    break;
                case IDictionary<string, object?> mutable when mutable.TryGetValue(segment, out var mutableValue):
                    current = mutableValue;
                    break;
                case System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.Object } element when element.TryGetProperty(segment, out var property):
                    current = property;
                    break;
                default:
                    return false;
            }
        }

        value = current;
        return true;
    }
    public static void SetValue(IDictionary<string, object?> context, string? path, object? value)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        IDictionary<string, object?> current = context;

        for (var index = 0; index < segments.Length - 1; index++)
        {
            var segment = segments[index];
            if (current.TryGetValue(segment, out var next) && next is IDictionary<string, object?> nested)
            {
                current = nested;
                continue;
            }

            var created = new Dictionary<string, object?>(StringComparer.Ordinal);
            current[segment] = created;
            current = created;
        }

        current[segments[^1]] = value;
    }

    public static bool ToBoolean(object? value)
    {
        return value switch
        {
            System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.Null or System.Text.Json.JsonValueKind.Undefined } => false,
            System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.True } => true,
            System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.False } => false,
            System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.String } element => ToBoolean(element.GetString()),
            System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.Number } element => element.TryGetDecimal(out var number) && number != 0,
            System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.Array } element => element.GetArrayLength() > 0,
            System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.Object } element => element.EnumerateObject().Any(),
            null => false,
            bool boolean => boolean,
            string text when bool.TryParse(text, out var parsed) => parsed,
            string text => !string.IsNullOrWhiteSpace(text),
            int number => number != 0,
            long number => number != 0L,
            double number => Math.Abs(number) > double.Epsilon,
            decimal number => number != 0,
            System.Collections.IDictionary dictionary => dictionary.Count > 0,
            System.Collections.IEnumerable sequence => sequence.Cast<object?>().Any(),
            _ => true,
        };
    }
}
