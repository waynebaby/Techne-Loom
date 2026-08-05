using System.Globalization;
using System.Text;
using System.Text.Json;
using NCalc;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public sealed class NCalcExpressionException : InvalidOperationException
{
    public NCalcExpressionException(string expression, string normalizedExpression, string phase, string detail, IReadOnlyCollection<string> availableContextPaths, Exception? innerException = null)
        : base($"NCalc expression {phase} failed. Expression: '{expression}'. Normalized NCalc expression: '{normalizedExpression}'. Detail: {detail}. Available context paths: {(availableContextPaths.Count == 0 ? "<none>" : string.Join(", ", availableContextPaths.OrderBy(static path => path, StringComparer.Ordinal)))}. Use NCalc syntax and ensure referenced data paths are present in context.", innerException)
    {
        Expression = expression;
        NormalizedExpression = normalizedExpression;
        Phase = phase;
        Detail = detail;
        AvailableContextPaths = availableContextPaths;
    }

    public string Expression { get; }
    public string NormalizedExpression { get; }
    public string Phase { get; }
    public string Detail { get; }
    public IReadOnlyCollection<string> AvailableContextPaths { get; }
}

public sealed class NCalcExpressionEvaluator : IExpressionEvaluator
{
    public object? Evaluate(string expression, IReadOnlyDictionary<string, object?> context)
    {
        var original = expression ?? string.Empty;
        var normalized = NormalizeExpression(original.Trim());
        if (!TryValidate(original, out var validationError))
        {
            throw CreateException(original, normalized, "evaluation", validationError, context);
        }

        try
        {
            var parsed = new Expression(normalized);
            foreach (var pair in FlattenContext(context))
            {
                parsed.Parameters[pair.Key] = pair.Value!;
            }

            foreach (var parameterName in parsed.GetParameterNames())
            {
                if (!parsed.Parameters.ContainsKey(parameterName))
                {
                    parsed.Parameters[parameterName] = null!;
                }
            }

            return parsed.Evaluate();
        }
        catch (Exception error)
        {
            throw CreateException(original, normalized, "evaluation", FormatError(error), context, error);
        }
    }

    public bool EvaluateBoolean(string expression, IReadOnlyDictionary<string, object?> context)
        => PathValueAccessor.ToBoolean(Evaluate(expression, context));

    public static bool IsWellFormedExpression(string? expression) => TryValidate(expression, out _);

    public static bool TryValidate(string? expression, out string diagnostic)
    {
        var normalized = NormalizeExpression((expression ?? string.Empty).Trim());
        if (normalized.Length == 0)
        {
            diagnostic = "Expression is empty. Provide a NCalc boolean expression, for example 'status == \'ready\'' or 'approved && score >= 60'.";
            return false;
        }

        try
        {
            var parsed = new Expression(normalized);
            if (parsed.HasErrors())
            {
                diagnostic = FormatError(parsed.Error);
                return false;
            }

            _ = parsed.GetParameterNames();
            diagnostic = string.Empty;
            return true;
        }
        catch (Exception error)
        {
            diagnostic = FormatError(error);
            return false;
        }
    }

    private static NCalcExpressionException CreateException(string expression, string normalized, string phase, string detail, IReadOnlyDictionary<string, object?> context, Exception? innerException = null)
        => new(expression, normalized, phase, detail, FlattenContext(context).Select(static pair => pair.Key).Distinct(StringComparer.Ordinal).ToArray(), innerException);

    private static string FormatError(Exception? error)
        => error is null ? "NCalc reported an unspecified parser error." : $"{error.GetType().Name}: {error.Message}";

    private static IEnumerable<KeyValuePair<string, object?>> FlattenContext(IReadOnlyDictionary<string, object?> context)
    {
        foreach (var pair in context)
        {
            foreach (var item in FlattenValue(pair.Key, pair.Value))
            {
                yield return item;
            }
        }
    }

    private static IEnumerable<KeyValuePair<string, object?>> FlattenValue(string path, object? value)
    {
        var converted = ConvertValue(value);
        yield return new(path, converted);
        if (value is JsonElement { ValueKind: JsonValueKind.Object } element)
        {
            foreach (var property in element.EnumerateObject())
            {
                foreach (var item in FlattenValue($"{path}.{property.Name}", property.Value)) yield return item;
            }
        }
        else if (value is IReadOnlyDictionary<string, object?> dictionary)
        {
            foreach (var pair in dictionary)
            {
                foreach (var item in FlattenValue($"{path}.{pair.Key}", pair.Value)) yield return item;
            }
        }
    }

    private static object? ConvertValue(object? value)
    {
        if (value is not JsonElement element) return value;
        return element.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var integer) => integer,
            JsonValueKind.Number when element.TryGetDecimal(out var decimalValue) => decimalValue,
            JsonValueKind.Array => element.EnumerateArray().Select(static item => ConvertValue(item)).ToArray(),
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(static property => property.Name, static property => ConvertValue(property.Value), StringComparer.Ordinal),
            _ => element.ToString(),
        };
    }

    private static string NormalizeExpression(string expression)
    {
        var output = new StringBuilder(expression.Length + 8);
        for (var index = 0; index < expression.Length;)
        {
            if (expression[index] == '[')
            {
                var end = expression.IndexOf(']', index + 1);
                if (end < 0) { output.Append(expression[index++]); continue; }
                output.Append(expression[index..(end + 1)]);
                index = end + 1;
                continue;
            }

            if (expression[index] is '\'' or '"')
            {
                var quote = expression[index++];
                output.Append(quote);
                while (index < expression.Length)
                {
                    var character = expression[index++];
                    output.Append(character);
                    if (character == quote && (index < 2 || expression[index - 2] != '\\')) break;
                }
                continue;
            }

            if (!IsIdentifierStart(expression[index])) { output.Append(expression[index++]); continue; }
            var start = index++;
            while (index < expression.Length && IsIdentifierPart(expression[index])) index++;
            var dotted = false;
            while (index < expression.Length && expression[index] == '.' && index + 1 < expression.Length && IsIdentifierStart(expression[index + 1]))
            {
                dotted = true;
                index += 2;
                while (index < expression.Length && IsIdentifierPart(expression[index])) index++;
            }
            var token = expression[start..index];
            output.Append(dotted ? $"[{token}]" : token);
        }
        return output.ToString();
    }

    private static bool IsIdentifierStart(char value) => char.IsLetter(value) || value == '_';
    private static bool IsIdentifierPart(char value) => char.IsLetterOrDigit(value) || value is '_' or '-';
}
