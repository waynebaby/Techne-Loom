namespace Techne.Loom.Common.TaskTracking.Runtime;

public interface IExpressionEvaluator
{
    object? Evaluate(string expression, IReadOnlyDictionary<string, object?> context);

    bool EvaluateBoolean(string expression, IReadOnlyDictionary<string, object?> context);
}