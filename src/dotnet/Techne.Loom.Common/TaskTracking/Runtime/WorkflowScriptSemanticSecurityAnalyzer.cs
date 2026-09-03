using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Techne.Loom.Common.TaskTracking.Runtime;

internal static class WorkflowScriptSemanticSecurityAnalyzer
{
    public static string? FindViolation(CSharpCompilation compilation, SyntaxTree syntaxTree)
        => RoslynCapabilityPolicy.FindScriptViolation(compilation, syntaxTree);
}
