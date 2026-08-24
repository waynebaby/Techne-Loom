using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public sealed class InferredTypeJsonConverter : JsonConverter<object?>
{
    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert == typeof(object);
    }

    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.Null => null,
            JsonTokenType.String => ReadString(ref reader),
            JsonTokenType.Number => ReadNumber(ref reader),
            JsonTokenType.StartObject => ReadObject(ref reader, options),
            JsonTokenType.StartArray => ReadArray(ref reader, options),
            _ => JsonDocument.ParseValue(ref reader).RootElement.Clone(),
        };
    }

    public override void Write(Utf8JsonWriter writer, object? value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, value?.GetType() ?? typeof(object), options);
    }

    private static object? ReadString(ref Utf8JsonReader reader)
    {
        var text = reader.GetString();
        var looksLikeIsoDateTime = !string.IsNullOrWhiteSpace(text)
            && (text.Contains('T', StringComparison.OrdinalIgnoreCase)
                || (text.Contains('-') && text.Contains(':')));
        if (looksLikeIsoDateTime
            && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dateTimeOffset))
        {
            return dateTimeOffset;
        }

        return text;
    }

    private static object ReadNumber(ref Utf8JsonReader reader)
    {
        if (reader.TryGetInt64(out var longValue))
        {
            return longValue;
        }

        if (reader.TryGetDecimal(out var decimalValue))
        {
            return decimalValue;
        }

        return reader.GetDouble();
    }

    private static Dictionary<string, object?> ReadObject(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var property in document.RootElement.EnumerateObject())
        {
            result[property.Name] = JsonSerializer.Deserialize<object?>(property.Value.GetRawText(), options);
        }

        return result;
    }

    private static List<object?> ReadArray(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var result = new List<object?>();

        foreach (var item in document.RootElement.EnumerateArray())
        {
            result.Add(JsonSerializer.Deserialize<object?>(item.GetRawText(), options));
        }

        return result;
    }
}