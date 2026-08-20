using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class ExpressionCompilerCacheTests
{
    [Fact]
    public void SuccessfulCompilationIsReused()
    {
        var compiler = new CountingCompiler();
        var cache = new ExpressionCompilerCache(compiler);
        var binding = new ExpressionBinding();
        var definition = new ExpressionDefinition { Source = "true" };

        var first = cache.Compile(binding, definition);
        var second = cache.Compile(binding, definition);

        Assert.Same(first, second);
        Assert.Equal(1, compiler.Count);
    }

    private sealed class CountingCompiler : IExpressionCompiler
    {
        public int Count { get; private set; }

        public ExpressionCompileResult Compile(ExpressionBinding binding, ExpressionDefinition definition, string field = "expression")
        {
            Count++;
            return ExpressionCompileResult.Succeeded(new ExpressionCompileFeedback { Status = "succeeded" }, static _ => true);
        }
    }
}
