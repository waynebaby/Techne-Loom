using System.Globalization;
using System.Text.Json;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public sealed class ContextFieldCatalog
{
    public const string CurrentSchemaVersion = "loom.context-fields.v1";

    private static readonly HashSet<string> SupportedTypes = new(StringComparer.Ordinal)
    {
        "string",
        "boolean",
        "integer",
        "number",
        "object",
        "array",
    };

    private static readonly HashSet<string> SupportedOwners = new(StringComparer.Ordinal)
    {
        "caller",
        "user",
        "agent",
        "runtime",
    };

    private readonly IReadOnlyDictionary<string, ContextFieldDefinition> _fields;

    private ContextFieldCatalog(string schemaVersion, IReadOnlyDictionary<string, ContextFieldDefinition> fields)
    {
        SchemaVersion = schemaVersion;
        _fields = fields;
    }

    public string SchemaVersion { get; }

    public IReadOnlyDictionary<string, ContextFieldDefinition> Fields => _fields;

    public bool TryGetField(string path, out ContextFieldDefinition field)
    {
        if (_fields.TryGetValue(path, out field!))
        {
            return true;
        }

        var separator = path.LastIndexOf(".", StringComparison.Ordinal);
        if (separator > 0
            && !path[(separator + 1)..].Contains('.', StringComparison.Ordinal)
            && _fields.TryGetValue(path[..separator], out var parent)
            && parent.Type == "object"
            && parent.ValueType is not null)
        {
            field = parent with
            {
                Path = path,
                Type = parent.ValueType,
                Nullable = false,
                ItemType = null,
                ValueType = null,
                AllowedValues = [],
                SuggestedValues = [],
                SuggestionReason = null,
            };
            return true;
        }

        field = null!;
        return false;
    }

    public void ValidateContextValues(
        IReadOnlyDictionary<string, object?> context,
        string sourcePath,
        IReadOnlySet<string>? allowedRoots = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        var declaredRoots = _fields.Keys
            .Select(static path => path.Split('.', 2)[0])
            .ToHashSet(StringComparer.Ordinal);
        if (allowedRoots is not null)
        {
            declaredRoots.UnionWith(allowedRoots);
        }
        foreach (var key in context.Keys)
        {
            var root = key.Split('.', 2)[0];
            if (!declaredRoots.Contains(root))
            {
                throw Invalid(sourcePath, $"Context root '{root}' is not declared by context_fields.");
            }

            if (key.Contains('.', StringComparison.Ordinal) && !TryGetField(key, out _))
            {
                throw Invalid(sourcePath, $"Top-level dotted context key '{key}' is not declared by context_fields.");
            }
        }

        foreach (var field in _fields.Values)
        {
            var hasDirectValue = context.TryGetValue(field.Path, out var directValue);
            object? nestedValue = null;
            var hasNestedValue = field.Path.Contains('.', StringComparison.Ordinal)
                && PathValueAccessor.TryGetNestedValue(context, field.Path, out nestedValue);
            if (hasDirectValue && hasNestedValue)
            {
                throw Invalid(sourcePath, $"Context path '{field.Path}' is supplied in both flat and nested form.");
            }

            if (hasDirectValue)
            {
                ValidateFieldValue(field.Path, directValue, sourcePath);
            }
            else if (hasNestedValue)
            {
                ValidateFieldValue(field.Path, nestedValue, sourcePath);
            }
        }

        ValidateDeclaredNestedMembers(context, sourcePath);
    }
    private void ValidateDeclaredNestedMembers(IReadOnlyDictionary<string, object?> context, string sourcePath)
    {
        foreach (var path in _fields.Keys.Where(static path => path.Contains('.', StringComparison.Ordinal)))
        {
            var segments = path.Split('.');
            for (var depth = 1; depth < segments.Length; depth++)
            {
                var parentPath = string.Join('.', segments.Take(depth));
                if (_fields.TryGetValue(parentPath, out var parentField) && parentField.ValueType is not null)
                {
                    continue;
                }

                if (!PathValueAccessor.TryGetValue(context, parentPath, out var parentValue)
                    || GetObjectProperties(parentValue) is not { } properties)
                {
                    continue;
                }

                var prefix = parentPath + ".";
                var declaredChildren = _fields.Keys
                    .Where(candidate => candidate.StartsWith(prefix, StringComparison.Ordinal))
                    .Select(candidate => candidate[prefix.Length..].Split('.')[0])
                    .ToHashSet(StringComparer.Ordinal);
                foreach (var property in properties)
                {
                    if (!declaredChildren.Contains(property.Key))
                    {
                        throw Invalid(sourcePath, $"Context path '{prefix}{property.Key}' is not declared by context_fields.");
                    }
                }
            }
        }
    }

    private static IEnumerable<KeyValuePair<string, object?>>? GetObjectProperties(object? value)
    {
        if (value is IReadOnlyDictionary<string, object?> readOnly)
        {
            return readOnly;
        }

        if (value is IDictionary<string, object?> mutable)
        {
            return mutable;
        }

        if (value is JsonElement { ValueKind: JsonValueKind.Object } element)
        {
            return element.EnumerateObject()
                .Select(static property => new KeyValuePair<string, object?>(property.Name, property.Value))
                .ToArray();
        }

        return null;
    }


    public void ValidateFieldValue(string path, object? value, string sourcePath)
    {
        if (!TryGetField(path, out var field))
        {
            throw Invalid(sourcePath, $"Context path '{path}' is not declared by context_fields.");
        }

        JsonElement element;
        try
        {
            element = JsonSerializer.SerializeToElement(value, value?.GetType() ?? typeof(object));
        }
        catch (Exception exception) when (exception is NotSupportedException or JsonException or InvalidOperationException)
        {
            throw Invalid(sourcePath, $"Context value for '{path}' cannot be represented as JSON: {exception.Message}");
        }

        ValidateValueType(path, field.Type, field.Nullable, field.ItemType, field.ValueType, element, sourcePath, "context");
        ValidateNumericBounds(path, field.Type, field.Minimum, field.Maximum, element, sourcePath, "context");
        if (field.AllowedValues.Count > 0
            && !field.AllowedValues.Any(allowed => JsonElement.DeepEquals(allowed, element)))
        {
            throw Invalid(sourcePath, $"Context value for '{path}' is not one of its declared allowed_values.");
        }
    }

    public static ContextFieldCatalog Parse(JsonElement contractRoot, string sourcePath)
    {
        if (contractRoot.ValueKind != JsonValueKind.Object
            || !contractRoot.TryGetProperty("context_fields", out var catalogElement)
            || catalogElement.ValueKind != JsonValueKind.Object)
        {
            throw Invalid(sourcePath, "A root context_fields object is required.");
        }

        var schemaVersion = ReadRequiredString(catalogElement, "schemaVersion", sourcePath, "context_fields");
        if (!string.Equals(schemaVersion, CurrentSchemaVersion, StringComparison.Ordinal))
        {
            throw Invalid(sourcePath, $"context_fields.schemaVersion '{schemaVersion}' is unsupported; expected '{CurrentSchemaVersion}'.");
        }

        if (!catalogElement.TryGetProperty("fields", out var fieldsElement)
            || fieldsElement.ValueKind != JsonValueKind.Array
            || fieldsElement.GetArrayLength() == 0)
        {
            throw Invalid(sourcePath, "context_fields.fields must be a non-empty array.");
        }

        var fields = new Dictionary<string, ContextFieldDefinition>(StringComparer.Ordinal);
        foreach (var fieldElement in fieldsElement.EnumerateArray())
        {
            if (fieldElement.ValueKind != JsonValueKind.Object)
            {
                throw Invalid(sourcePath, "Every context_fields.fields entry must be an object.");
            }

            var path = ReadRequiredString(fieldElement, "path", sourcePath, "context_fields.fields[]");
            ValidatePath(path, sourcePath);
            var type = ReadRequiredString(fieldElement, "type", sourcePath, $"context_fields.fields[{path}]");
            if (!SupportedTypes.Contains(type))
            {
                throw Invalid(sourcePath, $"Field '{path}' has unsupported JSON type '{type}'.");
            }

            if (!fieldElement.TryGetProperty("nullable", out var nullableElement)
                || nullableElement.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                throw Invalid(sourcePath, $"Field '{path}' must declare boolean nullable.");
            }

            var owner = ReadRequiredString(fieldElement, "owner", sourcePath, $"context_fields.fields[{path}]");
            if (!SupportedOwners.Contains(owner))
            {
                throw Invalid(sourcePath, $"Field '{path}' has unsupported owner '{owner}'.");
            }

            var description = ReadRequiredString(fieldElement, "description", sourcePath, $"context_fields.fields[{path}]");
            var itemType = ReadOptionalString(fieldElement, "itemType", sourcePath, path);
            var valueType = ReadOptionalString(fieldElement, "valueType", sourcePath, path);
            var minimum = ReadOptionalNumber(fieldElement, "minimum", sourcePath, path);
            var maximum = ReadOptionalNumber(fieldElement, "maximum", sourcePath, path);
            ValidateNumericBoundDeclarations(path, type, minimum, maximum, sourcePath);
            ValidateContainerTypes(path, type, itemType, valueType, sourcePath);

            var allowedValues = ReadJsonArray(fieldElement, "allowed_values", sourcePath, path);
            foreach (var value in allowedValues)
            {
                ValidateValueType(path, type, nullableElement.GetBoolean(), itemType, valueType, value, sourcePath, "allowed_values");
                ValidateNumericBounds(path, type, minimum, maximum, value, sourcePath, "allowed_values");
            }

            var suggestions = ReadSuggestions(fieldElement, path, type, nullableElement.GetBoolean(), itemType, valueType, minimum, maximum, sourcePath);
            if (allowedValues.Count > 0
                && suggestions.Any(suggestion => !allowedValues.Any(allowed => JsonElement.DeepEquals(allowed, suggestion.Value))))
            {
                throw Invalid(sourcePath, $"Field '{path}' suggested_values must be members of allowed_values.");
            }
            var suggestionReason = ReadOptionalString(fieldElement, "suggestion_reason", sourcePath, path);
            var definition = new ContextFieldDefinition(
                path,
                type,
                nullableElement.GetBoolean(),
                owner,
                description,
                itemType,
                valueType,
                minimum,
                maximum,
                allowedValues,
                suggestions,
                suggestionReason);
            if (!fields.TryAdd(path, definition))
            {
                throw Invalid(sourcePath, $"context_fields contains duplicate path '{path}'.");
            }
        }

        ValidateParentPaths(fields, sourcePath);
        return new ContextFieldCatalog(schemaVersion, fields);
    }

    private static void ValidateParentPaths(
        IReadOnlyDictionary<string, ContextFieldDefinition> fields,
        string sourcePath)
    {
        foreach (var field in fields.Values)
        {
            var separator = field.Path.IndexOf('.', StringComparison.Ordinal);
            while (separator >= 0)
            {
                var parentPath = field.Path[..separator];
                if (fields.TryGetValue(parentPath, out var parent)
                    && parent.Type != "object")
                {
                    throw Invalid(sourcePath, $"Field '{field.Path}' is nested beneath non-object field '{parentPath}'.");
                }

                separator = field.Path.IndexOf('.', separator + 1);
            }
        }
    }

    private static IReadOnlyList<ContextFieldSuggestion> ReadSuggestions(
        JsonElement fieldElement,
        string path,
        string type,
        bool nullable,
        string? itemType,
        string? valueType,
        decimal? minimum,
        decimal? maximum,
        string sourcePath)
    {
        if (!fieldElement.TryGetProperty("suggested_values", out var suggestionsElement))
        {
            return [];
        }

        if (suggestionsElement.ValueKind != JsonValueKind.Array)
        {
            throw Invalid(sourcePath, $"Field '{path}' suggested_values must be an array.");
        }

        var suggestions = new List<ContextFieldSuggestion>();
        foreach (var suggestionElement in suggestionsElement.EnumerateArray())
        {
            if (suggestionElement.ValueKind != JsonValueKind.Object
                || !suggestionElement.TryGetProperty("value", out var value))
            {
                throw Invalid(sourcePath, $"Every suggestion for field '{path}' must be an object with value.");
            }

            ValidateValueType(path, type, nullable, itemType, valueType, value, sourcePath, "suggested_values");
            ValidateNumericBounds(path, type, minimum, maximum, value, sourcePath, "suggested_values");
            suggestions.Add(new ContextFieldSuggestion(
                value.Clone(),
                ReadRequiredString(suggestionElement, "label", sourcePath, $"context_fields.fields[{path}].suggested_values[]"),
                ReadRequiredString(suggestionElement, "source", sourcePath, $"context_fields.fields[{path}].suggested_values[]"),
                ReadRequiredString(suggestionElement, "kind", sourcePath, $"context_fields.fields[{path}].suggested_values[]")));
        }

        return suggestions;
    }
    private static IReadOnlyList<JsonElement> ReadJsonArray(
        JsonElement element,
        string propertyName,
        string sourcePath,
        string fieldPath)
    {
        if (!element.TryGetProperty(propertyName, out var values))
        {
            return [];
        }

        if (values.ValueKind != JsonValueKind.Array)
        {
            throw Invalid(sourcePath, $"Field '{fieldPath}' {propertyName} must be an array.");
        }

        return values.EnumerateArray().Select(static value => value.Clone()).ToArray();
    }

    private static void ValidateValueType(
        string path,
        string type,
        bool nullable,
        string? itemType,
        string? valueType,
        JsonElement value,
        string sourcePath,
        string collectionName)
    {
        if (value.ValueKind == JsonValueKind.Null)
        {
            if (!nullable)
            {
                throw Invalid(sourcePath, $"Field '{path}' {collectionName} cannot contain null because nullable is false.");
            }

            return;
        }

        var matches = type switch
        {
            "string" => value.ValueKind == JsonValueKind.String,
            "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
            "number" => value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out _),
            "object" => value.ValueKind == JsonValueKind.Object,
            "array" => value.ValueKind == JsonValueKind.Array,
            _ => false,
        };
        if (!matches)
        {
            throw Invalid(sourcePath, $"Field '{path}' {collectionName} value does not match JSON type '{type}'.");
        }

        if (type == "array" && itemType is not null)
        {
            foreach (var item in value.EnumerateArray())
            {
                ValidateScalarType(path, itemType, item, sourcePath, $"{collectionName} item");
            }
        }
        else if (type == "object" && valueType is not null)
        {
            foreach (var property in value.EnumerateObject())
            {
                ValidateScalarType(path, valueType, property.Value, sourcePath, $"{collectionName} map value '{property.Name}'");
            }
        }
    }

    private static void ValidateScalarType(
        string path,
        string type,
        JsonElement value,
        string sourcePath,
        string valueDescription)
    {
        var matches = type switch
        {
            "string" => value.ValueKind == JsonValueKind.String,
            "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
            "number" => value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out _),
            "object" => value.ValueKind == JsonValueKind.Object,
            "array" => value.ValueKind == JsonValueKind.Array,
            _ => false,
        };
        if (!matches)
        {
            throw Invalid(sourcePath, $"Field '{path}' {valueDescription} does not match JSON type '{type}'.");
        }
    }

    private static void ValidateContainerTypes(
        string path,
        string type,
        string? itemType,
        string? valueType,
        string sourcePath)
    {
        if (itemType is not null && !SupportedTypes.Contains(itemType))
        {
            throw Invalid(sourcePath, $"Field '{path}' declares unsupported itemType '{itemType}'.");
        }

        if (valueType is not null && !SupportedTypes.Contains(valueType))
        {
            throw Invalid(sourcePath, $"Field '{path}' declares unsupported valueType '{valueType}'.");
        }

        if (type == "array" && itemType is null)
        {
            throw Invalid(sourcePath, $"Array field '{path}' must declare itemType.");
        }

        if (type != "array" && itemType is not null)
        {
            throw Invalid(sourcePath, $"Non-array field '{path}' cannot declare itemType.");
        }

        if (valueType is not null && type != "object")
        {
            throw Invalid(sourcePath, $"Only object fields may declare valueType; '{path}' has type '{type}'.");
        }
    }

    private static string ReadRequiredString(JsonElement element, string propertyName, string sourcePath, string location)
    {
        if (!element.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(property.GetString()))
        {
            throw Invalid(sourcePath, $"{location}.{propertyName} must be a non-empty string.");
        }

        return property.GetString()!;
    }

    private static decimal? ReadOptionalNumber(JsonElement fieldElement, string propertyName, string sourcePath, string path)
    {
        if (!fieldElement.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.Number || !value.TryGetDecimal(out var number))
        {
            throw Invalid(sourcePath, $"Field '{path}' {propertyName} must be a representable JSON number.");
        }

        return number;
    }

    private static void ValidateNumericBoundDeclarations(string path, string type, decimal? minimum, decimal? maximum, string sourcePath)
    {
        if ((minimum is not null || maximum is not null) && type is not ("integer" or "number"))
        {
            throw Invalid(sourcePath, $"Field '{path}' minimum and maximum are supported only for integer or number fields.");
        }

        if (type == "integer"
            && ((minimum is { } min && decimal.Truncate(min) != min)
                || (maximum is { } max && decimal.Truncate(max) != max)))
        {
            throw Invalid(sourcePath, $"Field '{path}' integer minimum and maximum must be whole numbers.");
        }

        if (minimum is { } lowerBound && maximum is { } upperBound && lowerBound > upperBound)
        {
            throw Invalid(sourcePath, $"Field '{path}' minimum must not exceed maximum.");
        }
    }

    private static void ValidateNumericBounds(
        string path,
        string type,
        decimal? minimum,
        decimal? maximum,
        JsonElement value,
        string sourcePath,
        string collectionName)
    {
        if (value.ValueKind == JsonValueKind.Null || type is not ("integer" or "number") || !value.TryGetDecimal(out var number))
        {
            return;
        }

        if (minimum is { } lowerBound && number < lowerBound)
        {
            throw Invalid(sourcePath, $"Field '{path}' {collectionName} value is below minimum {lowerBound.ToString(CultureInfo.InvariantCulture)}.");
        }

        if (maximum is { } upperBound && number > upperBound)
        {
            throw Invalid(sourcePath, $"Field '{path}' {collectionName} value is above maximum {upperBound.ToString(CultureInfo.InvariantCulture)}.");
        }
    }

    private static string? ReadOptionalString(JsonElement element, string propertyName, string sourcePath, string path)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(property.GetString()))
        {
            throw Invalid(sourcePath, $"Field '{path}' {propertyName} must be a non-empty string when declared.");
        }

        return property.GetString();
    }

    private static void ValidatePath(string path, string sourcePath)
    {
        var segments = path.Split('.');
        if (path.StartsWith(".", StringComparison.Ordinal)
            || path.EndsWith(".", StringComparison.Ordinal)
            || segments.Any(string.IsNullOrWhiteSpace)
            || segments.Any(segment => !string.Equals(segment, segment.Trim(), StringComparison.Ordinal)))
        {
            throw Invalid(sourcePath, $"Context field path '{path}' must be a non-empty dotted path.");
        }
    }

    private static InvalidOperationException Invalid(string sourcePath, string message)
        => new($"Context field catalog '{sourcePath}' is invalid: {message}");
}
