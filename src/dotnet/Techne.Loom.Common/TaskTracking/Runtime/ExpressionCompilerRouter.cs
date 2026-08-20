using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public interface IExpressionCompiler
{
    ExpressionCompileResult Compile(ExpressionBinding binding, ExpressionDefinition definition, string field = "expression");
}

public sealed class ExpressionCompilerRouter : IExpressionCompiler
{
    private readonly CSharpExpressionCompiler _csharpCompiler;

    public ExpressionCompilerRouter(CSharpExpressionCompiler? csharpCompiler = null)
    {
        _csharpCompiler = csharpCompiler ?? new CSharpExpressionCompiler();
    }

    public ExpressionCompileResult Compile(ExpressionBinding binding, ExpressionDefinition definition, string field = "expression")
    {
        if (string.Equals(binding.Language, ExpressionContract.CurrentLanguage, StringComparison.Ordinal))
        {
            return _csharpCompiler.Compile(binding, definition, field);
        }

        var feedback = new ExpressionCompileFeedback
        {
            Status = "failed",
            Language = binding.Language,
            LanguageVersion = binding.LanguageVersion,
            ContractId = binding.ContractId,
            ContractVersion = binding.ContractVersion,
            Field = field,
            DiagnosticCode = "LOOM.EXPR.ROUTER.UNSUPPORTED_LANGUAGE",
            DiagnosticCategory = "contract",
            Severity = "error",
            Message = $"Expression language '{binding.Language}' is not supported by this runtime.",
            SuggestedFix = "Use csharp for the current .NET runtime or translate the expression before changing runtime routes.",
            CompilerIdentity = "Techne.Loom.ExpressionCompilerRouter",
        };
        return ExpressionCompileResult.Failed(feedback);
    }
}
