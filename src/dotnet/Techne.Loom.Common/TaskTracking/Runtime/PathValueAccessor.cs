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

        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        object? current = context;

        foreach (var segment in segments)
        {
            if (current is IReadOnlyDictionary<string, object?> readOnly)
            {
                if (!readOnly.TryGetValue(segment, out current))
                {
                    return false;
                }

                continue;
            }

            if (current is IDictionary<string, object?> mutable)
            {
                if (!mutable.TryGetValue(segment, out current))
                {
                    return false;
                }

                continue;
            }

            if (current is System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.Object } element)
            {
                if (!element.TryGetProperty(segment, out var property))
                {
                    return false;
                }

                current = property;
                continue;
            }

            return false;
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
