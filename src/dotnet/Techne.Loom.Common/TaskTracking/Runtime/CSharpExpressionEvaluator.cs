using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public sealed class CSharpExpressionEvaluator : IExpressionEvaluator
{
    private readonly ExpressionCompilerCache _compiler;
    private readonly ExpressionBinding _binding;

    public CSharpExpressionEvaluator(ExpressionBinding? binding = null)
    {
        _binding = binding ?? new ExpressionBinding();
        _compiler = new ExpressionCompilerCache(new ExpressionCompilerRouter());
    }

    public object? Evaluate(string expression, IReadOnlyDictionary<string, object?> context)
        => Evaluate(expression, context, _binding);

    public object? Evaluate(string expression, IReadOnlyDictionary<string, object?> context, ExpressionBinding binding)
    {
        var definition = new ExpressionDefinition { Source = expression };
        var result = _compiler.Compile(binding, definition);
        if (!result.IsSuccess)
        {
            throw new CSharpExpressionException(expression, result.Feedback);
        }

        return result.Execute!(new ExpressionRuntimeContext(context));
    }

    public bool EvaluateBoolean(string expression, IReadOnlyDictionary<string, object?> context)
        => EvaluateBoolean(expression, context, _binding);

    public bool EvaluateBoolean(string expression, IReadOnlyDictionary<string, object?> context, ExpressionBinding binding)
        => PathValueAccessor.ToBoolean(Evaluate(expression, context, binding));
}

public sealed class CSharpExpressionException : InvalidOperationException
{
    public CSharpExpressionException(string expression, ExpressionCompileFeedback feedback)
        : base($"C# expression evaluation failed. Expression: '{expression}'. Diagnostic: {feedback.DiagnosticCode}. {feedback.Message}")
    {
        Expression = expression;
        Feedback = feedback;
    }

    public string Expression { get; }

    public ExpressionCompileFeedback Feedback { get; }
}
