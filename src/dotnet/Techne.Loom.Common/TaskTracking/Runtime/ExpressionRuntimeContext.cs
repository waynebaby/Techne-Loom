using System.Globalization;
using System.Text.Json;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public sealed class ExpressionRuntimeContext
{
    private readonly IReadOnlyDictionary<string, object?> _values;

    public ExpressionRuntimeContext(IReadOnlyDictionary<string, object?> values)
    {
        _values = values;
    }

    public bool Has(string path) => TryGet(path, out _);

    public object? this[string path] => TryGet(path, out var value) ? value : null;

    public T? Get<T>(string path)
    {
        if (!TryGet(path, out var value) || value is null)
        {
            return default;
        }

        if (value is JsonElement element)
        {
            value = element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Number when element.TryGetInt64(out var integer) => integer,
                JsonValueKind.Number when element.TryGetDecimal(out var decimalValue) => decimalValue,
                _ => value,
            };
        }

        if (value is T typed)
        {
            return typed;
        }

        return (T?)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
    }

    private bool TryGet(string path, out object? value)
    {
        if (_values.TryGetValue(path, out value))
        {
            return true;
        }

        object? current = _values;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            switch (current)
            {
                case IReadOnlyDictionary<string, object?> readOnlyDictionary when readOnlyDictionary.TryGetValue(segment, out value):
                    current = value;
                    break;
                case IDictionary<string, object?> dictionary when dictionary.TryGetValue(segment, out value):
                    current = value;
                    break;
                case JsonElement { ValueKind: JsonValueKind.Object } element when element.TryGetProperty(segment, out var property):
                    value = property;
                    current = property;
                    break;
                default:
                    value = null;
                    return false;
            }

        }

        return true;
    }
}
