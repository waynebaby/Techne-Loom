using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public interface IExpressionEvaluator
{
    object? Evaluate(string expression, IReadOnlyDictionary<string, object?> context);

    object? Evaluate(string expression, IReadOnlyDictionary<string, object?> context, ExpressionBinding binding)
        => Evaluate(expression, context);

    bool EvaluateBoolean(string expression, IReadOnlyDictionary<string, object?> context);

    bool EvaluateBoolean(string expression, IReadOnlyDictionary<string, object?> context, ExpressionBinding binding)
        => EvaluateBoolean(expression, context);
}