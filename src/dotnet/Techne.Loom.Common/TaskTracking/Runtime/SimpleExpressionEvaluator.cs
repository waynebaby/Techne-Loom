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
            var leftValue = PathValueAccessor.GetValue(context, left);
            var rightValue = ParseLiteral(right, context);
            var equals = AreEqual(leftValue, rightValue);
            return op == "==" ? equals : !equals;
        }

        return PathValueAccessor.GetValue(context, expression);
    }

    public bool EvaluateBoolean(string expression, IReadOnlyDictionary<string, object?> context)
        => PathValueAccessor.ToBoolean(Evaluate(expression, context));

    private static object? ParseLiteral(string token, IReadOnlyDictionary<string, object?> context)
    {
        if (string.Equals(token, "null", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (token.StartsWith('"') && token.EndsWith('"') && token.Length >= 2)
        {
            return token[1..^1];
        }

        if (token.StartsWith('\'') && token.EndsWith('\'') && token.Length >= 2)
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

        return PathValueAccessor.GetValue(context, token) ?? token;
    }

    private static bool AreEqual(object? left, object? right)
    {
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
}