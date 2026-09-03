using System.Collections;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public sealed class ExpressionRuntimeContext
{
    private const int MaxCollectionItems = 32;
    private const int MaxCollectionProjectedBytes = 32 * 1024;
    private const int MaxContextPathSegments = 6;

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

        if (TryGetCollectionElementType(typeof(T), out var elementType))
        {
            return (T)MaterializeBoundedCollection(value, typeof(T), elementType);
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
        EnsurePathDepth(path);
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

    private static object MaterializeBoundedCollection(object value, Type targetType, Type elementType)
    {
        var values = value is JsonElement jsonElement
            ? ReadJsonArray(jsonElement, elementType)
            : ReadClrCollection(value, elementType);
        var list = new List<object?>(values);
        if (list.Count > MaxCollectionItems)
        {
            throw new ExpressionResourceLimitException("LOOM.EXPR.RESOURCE.COLLECTION_ITEMS", "The context collection exceeds the 32-item expression limit.");
        }

        var projectedBytes = list.Sum(EstimateProjectedBytes);
        if (projectedBytes > MaxCollectionProjectedBytes)
        {
            throw new ExpressionResourceLimitException("LOOM.EXPR.RESOURCE.COLLECTION_BYTES", "The context collection exceeds the 32 KiB expression limit.");
        }

        if (targetType.IsArray)
        {
            var array = Array.CreateInstance(elementType, list.Count);
            for (var index = 0; index < list.Count; index++)
            {
                array.SetValue(list[index], index);
            }

            return array;
        }

        var typedList = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;
        foreach (var item in list)
        {
            typedList.Add(item);
        }

        return typedList;
    }

    private static IEnumerable<object?> ReadJsonArray(JsonElement element, Type elementType)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw new ExpressionResourceLimitException("LOOM.EXPR.RESOURCE.COLLECTION_SHAPE", "The context value is not a JSON array.");
        }

        if (element.GetArrayLength() > MaxCollectionItems)
        {
            throw new ExpressionResourceLimitException("LOOM.EXPR.RESOURCE.COLLECTION_ITEMS", "The context JSON array exceeds the 32-item expression limit.");
        }

        return element.EnumerateArray().Select(item => ConvertJsonScalar(item, elementType)).ToArray();
    }

    private static IEnumerable<object?> ReadClrCollection(object value, Type elementType)
    {
        if (value is not Array && value is not IList)
        {
            throw new ExpressionResourceLimitException("LOOM.EXPR.RESOURCE.COLLECTION_SHAPE", "Only arrays and concrete list values can be used as expression collections.");
        }

        if (value is Array array)
        {
            if (array.Length > 32)
            {
                throw new ExpressionResourceLimitException("LOOM.EXPR.RESOURCE.COLLECTION_ITEMS", "The context collection exceeds the 32-item expression limit.");
            }

            return array.Cast<object?>().Select(item => ConvertClrScalar(item, elementType)).ToArray();
        }

        var list = (IList)value;
        if (list.Count > 32)
        {
            throw new ExpressionResourceLimitException("LOOM.EXPR.RESOURCE.COLLECTION_ITEMS", "The context collection exceeds the 32-item expression limit.");
        }

        return list.Cast<object?>().Select(item => ConvertClrScalar(item, elementType)).ToArray();
    }

    private static object? ConvertJsonScalar(JsonElement element, Type targetType)
    {
        if (element.ValueKind is JsonValueKind.Array or JsonValueKind.Object)
        {
            throw new ExpressionResourceLimitException("LOOM.EXPR.RESOURCE.COLLECTION_SHAPE", "Expression collections may contain only primitive or string values.");
        }

        if (element.ValueKind == JsonValueKind.Null)
        {
            if (targetType.IsValueType && Nullable.GetUnderlyingType(targetType) is null)
            {
                throw new ExpressionResourceLimitException("LOOM.EXPR.RESOURCE.COLLECTION_SHAPE", $"The JSON collection element cannot be null for {targetType.Name}.");
            }

            return null;
        }

        var nullableType = Nullable.GetUnderlyingType(targetType);
        var effectiveType = nullableType ?? targetType;
        if (effectiveType == typeof(string) && element.ValueKind == JsonValueKind.String)
        {
            return element.GetString();
        }

        if (effectiveType == typeof(bool) && element.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return element.GetBoolean();
        }

        if (effectiveType == typeof(int) && element.TryGetInt32(out var integer))
        {
            return integer;
        }

        if (effectiveType == typeof(long) && element.TryGetInt64(out var longValue))
        {
            return longValue;
        }

        if (effectiveType == typeof(decimal) && element.TryGetDecimal(out var decimalValue))
        {
            return decimalValue;
        }

        if (effectiveType == typeof(double) && element.TryGetDouble(out var doubleValue))
        {
            return doubleValue;
        }

        throw new ExpressionResourceLimitException("LOOM.EXPR.RESOURCE.COLLECTION_SHAPE", $"The JSON collection element cannot be converted to {targetType.Name}.");
    }

    private static object? ConvertClrScalar(object? value, Type targetType)
    {
        if (value is null)
        {
            if (targetType.IsValueType && Nullable.GetUnderlyingType(targetType) is null)
            {
                throw new ExpressionResourceLimitException("LOOM.EXPR.RESOURCE.COLLECTION_SHAPE", $"The context collection element cannot be null for {targetType.Name}.");
            }

            return null;
        }

        if (value is JsonElement element)
        {
            return ConvertJsonScalar(element, targetType);
        }

        var nullableType = Nullable.GetUnderlyingType(targetType);
        var effectiveType = nullableType ?? targetType;
        if (effectiveType.IsInstanceOfType(value))
        {
            return value;
        }

        try
        {
            return Convert.ChangeType(value, effectiveType, CultureInfo.InvariantCulture);
        }
        catch (Exception exception) when (exception is InvalidCastException or FormatException or OverflowException)
        {
            throw new ExpressionResourceLimitException("LOOM.EXPR.RESOURCE.COLLECTION_SHAPE", $"The context collection element cannot be converted to {targetType.Name}.", exception);
        }
    }

    private static bool TryGetCollectionElementType(Type targetType, out Type elementType)
    {
        if (targetType.IsArray)
        {
            elementType = targetType.GetElementType()!;
            return IsSupportedCollectionElement(elementType);
        }

        if (targetType.IsGenericType
            && targetType.GetGenericTypeDefinition() is var definition
            && definition is not null
            && definition == typeof(IReadOnlyList<>).GetGenericTypeDefinition())
        {
            elementType = targetType.GetGenericArguments()[0];
            return IsSupportedCollectionElement(elementType);
        }

        if (targetType.IsGenericType
            && targetType.GetGenericTypeDefinition() is var genericDefinition
            && genericDefinition is not null
            && genericDefinition == typeof(IReadOnlyCollection<>).GetGenericTypeDefinition())
        {
            elementType = targetType.GetGenericArguments()[0];
            return IsSupportedCollectionElement(elementType);
        }

        if (targetType.IsGenericType
            && targetType.GetGenericTypeDefinition() is var enumerableDefinition
            && enumerableDefinition is not null
            && enumerableDefinition == typeof(IEnumerable<>).GetGenericTypeDefinition())
        {
            elementType = targetType.GetGenericArguments()[0];
            return IsSupportedCollectionElement(elementType);
        }

        elementType = typeof(object);
        return false;
    }

    private static bool IsSupportedCollectionElement(Type elementType)
    {
        var effectiveType = Nullable.GetUnderlyingType(elementType) ?? elementType;
        return effectiveType == typeof(bool)
            || effectiveType == typeof(byte)
            || effectiveType == typeof(sbyte)
            || effectiveType == typeof(short)
            || effectiveType == typeof(ushort)
            || effectiveType == typeof(int)
            || effectiveType == typeof(uint)
            || effectiveType == typeof(long)
            || effectiveType == typeof(ulong)
            || effectiveType == typeof(float)
            || effectiveType == typeof(double)
            || effectiveType == typeof(decimal)
            || effectiveType == typeof(string);
    }

    private static int EstimateProjectedBytes(object? value)
    {
        if (value is null)
        {
            return 1;
        }

        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        return Encoding.UTF8.GetByteCount(text);
    }

    private static void EnsurePathDepth(string path)
    {
        if (path.Split('.', StringSplitOptions.RemoveEmptyEntries).Length > MaxContextPathSegments)
        {
            throw new ExpressionResourceLimitException("LOOM.EXPR.RESOURCE.CONTEXT_DEPTH", "The context path exceeds the six-level expression limit.");
        }
    }
}

public sealed class ExpressionResourceLimitException : InvalidOperationException
{
    public ExpressionResourceLimitException(string diagnosticCode, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        DiagnosticCode = diagnosticCode;
    }

    public string DiagnosticCode { get; }
}
