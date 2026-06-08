namespace Techne.Loom.Common.TaskTracking.Runtime;

public static class PathValueAccessor
{
    public static object? GetValue(IReadOnlyDictionary<string, object?> context, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        object? current = context;

        foreach (var segment in segments)
        {
            if (current is IReadOnlyDictionary<string, object?> readOnly)
            {
                if (!readOnly.TryGetValue(segment, out current))
                {
                    return null;
                }

                continue;
            }

            if (current is IDictionary<string, object?> mutable)
            {
                if (!mutable.TryGetValue(segment, out current))
                {
                    return null;
                }

                continue;
            }

            return null;
        }

        return current;
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
            null => false,
            bool boolean => boolean,
            string text when bool.TryParse(text, out var parsed) => parsed,
            string text => !string.IsNullOrWhiteSpace(text),
            int number => number != 0,
            long number => number != 0L,
            double number => Math.Abs(number) > double.Epsilon,
            _ => true,
        };
    }
}