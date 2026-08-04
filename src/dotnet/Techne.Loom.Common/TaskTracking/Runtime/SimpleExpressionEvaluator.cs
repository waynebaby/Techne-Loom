using System.Globalization;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public sealed class SimpleExpressionEvaluator : IExpressionEvaluator
{
    public object? Evaluate(string expression, IReadOnlyDictionary<string, object?> context)
    {
        expression = (expression ?? string.Empty).Trim();
        if (expression.Length == 0)
        {
            return false;
        }

        var orParts = SplitLogicalExpression(expression, "||");
        if (orParts.Count > 1)
        {
            foreach (var part in orParts)
            {
                if (EvaluateBoolean(part, context))
                {
                    return true;
                }
            }

            return false;
        }

        var andParts = SplitLogicalExpression(expression, "&&");
        if (andParts.Count > 1)
        {
            foreach (var part in andParts)
            {
                if (!EvaluateBoolean(part, context))
                {
                    return false;
                }
            }

            return true;
        }

        if (string.Equals(expression, "true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(expression, "false", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (expression.StartsWith('!'))
        {
            return !EvaluateBoolean(expression[1..], context);
        }

        foreach (var op in new[] { "==", "!=" })
        {
            var index = expression.IndexOf(op, StringComparison.Ordinal);
            if (index <= 0)
            {
                continue;
            }

            var left = expression[..index].Trim();
            var right = expression[(index + op.Length)..].Trim();
            var leftResolved = PathValueAccessor.TryGetValue(context, left, out var leftValue);
            var rightValue = ParseLiteral(right, context, out var rightResolved);
            if (!leftResolved && rightResolved && IsNullLike(rightValue))
            {
                return op == "==";
            }

            if (!leftResolved || !rightResolved)
            {
                return false;
            }

            var equals = AreEqual(leftValue, rightValue);
            return op == "==" ? equals : !equals;
        }

        return PathValueAccessor.GetValue(context, expression);
    }

    public bool EvaluateBoolean(string expression, IReadOnlyDictionary<string, object?> context)
        => PathValueAccessor.ToBoolean(Evaluate(expression, context));

    public static bool IsWellFormedExpression(string? expression)
    {
        expression = (expression ?? string.Empty).Trim();
        if (expression.Length == 0) return false;
        var orParts = SplitLogicalExpression(expression, "||");
        if (orParts.Count > 1) return orParts.All(IsWellFormedExpression);
        var andParts = SplitLogicalExpression(expression, "&&");
        if (andParts.Count > 1) return andParts.All(IsWellFormedExpression);
        if (string.Equals(expression, "true", StringComparison.OrdinalIgnoreCase) || string.Equals(expression, "false", StringComparison.OrdinalIgnoreCase)) return true;
        if (expression.StartsWith('!')) return IsWellFormedExpression(expression[1..]);
        foreach (var op in new[] { "==", "!=" })
        {
            var index = expression.IndexOf(op, StringComparison.Ordinal);
            if (index > 0 && expression.IndexOf(op, index + op.Length, StringComparison.Ordinal) < 0)
            {
                return IsPathToken(expression[..index].Trim()) && IsLiteralOrPath(expression[(index + op.Length)..].Trim());
            }
        }
        return IsPathToken(expression);
    }

    private static bool IsLiteralOrPath(string token)
    {
        if (string.Equals(token, "null", StringComparison.OrdinalIgnoreCase) || bool.TryParse(token, out _) || long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out _) || double.TryParse(token, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out _)) return true;
        if ((token.StartsWith("'") && token.EndsWith("'")) || (token.StartsWith("\"") && token.EndsWith("\""))) return token.Length >= 2;
        return IsPathToken(token);
    }

    private static bool IsPathToken(string token)
    {
        var segments = token.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0) return false;
        foreach (var segment in segments)
        {
            if (!(char.IsLetter(segment[0]) || segment[0] == '_')) return false;
            if (segment.Skip(1).Any(character => !(char.IsLetterOrDigit(character) || character is '_' or '-'))) return false;
        }
        return true;
    }

    private static object? ParseLiteral(string token, IReadOnlyDictionary<string, object?> context, out bool resolved)
    {
        resolved = true;
        if (string.Equals(token, "null", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (token.StartsWith("\"", StringComparison.Ordinal) && token.EndsWith("\"", StringComparison.Ordinal) && token.Length >= 2)
        {
            return token[1..^1];
        }

        if (token.StartsWith("'", StringComparison.Ordinal) && token.EndsWith("'", StringComparison.Ordinal) && token.Length >= 2)
        {
            return token[1..^1];
        }

        if (bool.TryParse(token, out var boolean))
        {
            return boolean;
        }

        if (long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
        {
            return integer;
        }

        if (double.TryParse(token, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var floating))
        {
            return floating;
        }

        if (PathValueAccessor.TryGetValue(context, token, out var value))
        {
            return value;
        }

        resolved = false;
        return null;
    }


    private static bool AreEqual(object? left, object? right)
    {
        left = NormalizeValue(left);
        right = NormalizeValue(right);

        if (left is null && right is null)
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        if (left.Equals(right))
        {
            return true;
        }

        return string.Equals(Convert.ToString(left, CultureInfo.InvariantCulture), Convert.ToString(right, CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    private static bool IsNullLike(object? value)
        => NormalizeValue(value) is null;

    private static object? NormalizeValue(object? value)
        => value is System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.Null or System.Text.Json.JsonValueKind.Undefined }
            ? null
            : value;
    private static IReadOnlyList<string> SplitLogicalExpression(string expression, string operatorToken)
    {
        var parts = new List<string>();
        var start = 0;
        var quote = '\0';

        for (var index = 0; index <= expression.Length - operatorToken.Length; index++)
        {
            var character = expression[index];
            if (quote != '\0')
            {
                if (character == quote && (index == 0 || expression[index - 1] != '\\'))
                {
                    quote = '\0';
                }

                continue;
            }

            if (character is '\'' or '"')
            {
                quote = character;
                continue;
            }

            if (!expression.AsSpan(index, operatorToken.Length).SequenceEqual(operatorToken.AsSpan()))
            {
                continue;
            }

            parts.Add(expression[start..index].Trim());
            start = index + operatorToken.Length;
            index += operatorToken.Length - 1;
        }

        if (parts.Count == 0)
        {
            return [expression];
        }

        parts.Add(expression[start..].Trim());
        return parts;
    }
}
