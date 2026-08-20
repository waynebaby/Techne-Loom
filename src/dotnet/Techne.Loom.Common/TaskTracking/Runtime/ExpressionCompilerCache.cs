using System.Collections.Concurrent;
using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public sealed class ExpressionCompilerCache : IExpressionCompiler
{
    private readonly IExpressionCompiler _compiler;
    private readonly int _capacity;
    private readonly ConcurrentDictionary<string, Lazy<ExpressionCompileResult>> _entries = new(StringComparer.Ordinal);

    public ExpressionCompilerCache(IExpressionCompiler compiler, int capacity = 256)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        _compiler = compiler;
        _capacity = capacity;
    }

    public ExpressionCompileResult Compile(ExpressionBinding binding, ExpressionDefinition definition, string field = "expression")
    {
        var key = string.Join("\u001f", binding.Language, binding.LanguageVersion, binding.ContractId, binding.ContractVersion, binding.CompileFeedbackContract, string.Join(",", binding.RequiredExpressionCapabilities), definition.Kind, definition.Source, definition.EntryPoint ?? string.Empty, definition.ResultType);
        var entry = _entries.GetOrAdd(key, _ => new Lazy<ExpressionCompileResult>(() => _compiler.Compile(binding, definition, field), LazyThreadSafetyMode.ExecutionAndPublication));
        var result = entry.Value;
        if (!result.IsSuccess)
        {
            _entries.TryRemove(key, out _);
            return result;
        }

        while (_entries.Count > _capacity && _entries.TryRemove(_entries.Keys.First(), out _))
        {
        }

        return result;
    }
}
