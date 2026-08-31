using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public sealed class CSharpExpressionCompiler
{
    private const string CompilerIdentity = "Microsoft.CodeAnalysis.CSharp";
    private const int MaxExpressionDiagnostics = 32;

    public ExpressionCompileResult Compile(ExpressionBinding binding, ExpressionDefinition definition, string field = "expression")
    {
        var feedback = CreateFeedback(binding, definition, field);
        if (!string.Equals(binding.Language, ExpressionContract.CurrentLanguage, StringComparison.Ordinal)
            || !string.Equals(binding.CompileFeedbackContract, ExpressionContract.DetailedCompileFeedbackContract, StringComparison.Ordinal))
        {
            feedback.Status = "failed";
            feedback.DiagnosticCode = "LOOM.EXPR.CONTRACT.UNSUPPORTED_BINDING";
            feedback.DiagnosticCategory = "contract";
            feedback.Severity = "error";
            feedback.Message = "The expression binding is not supported by the C# compiler.";
            feedback.SuggestedFix = "Use language csharp and compileFeedbackContract detailedCompileFeedbackV1.";
            AddPrimaryDiagnostic(feedback);
            return ExpressionCompileResult.Failed(feedback);
        }

        if (!string.Equals(definition.Kind, "predicate", StringComparison.Ordinal))
        {
            feedback.Status = "failed";
            feedback.DiagnosticCode = "LOOM.EXPR.CONTRACT.UNSUPPORTED_KIND";
            feedback.DiagnosticCategory = "contract";
            feedback.Severity = "error";
            feedback.Message = $"Expression kind '{definition.Kind}' is not implemented yet.";
            feedback.SuggestedFix = "Use kind predicate until lambda and method forms are implemented.";
            AddPrimaryDiagnostic(feedback);
            return ExpressionCompileResult.Failed(feedback);
        }

        if (definition.Source.Contains("async", StringComparison.Ordinal)
            || definition.Source.Contains("await", StringComparison.Ordinal)
            || definition.Source.Contains("Task", StringComparison.Ordinal))
        {
            feedback.Status = "failed";
            feedback.DiagnosticCode = "LOOM.EXPR.SECURITY.SYNCHRONOUS_ONLY";
            feedback.DiagnosticCategory = "security";
            feedback.Severity = "error";
            feedback.Message = "Asynchronous expressions are not supported.";
            feedback.SuggestedFix = "Remove async, await, and Task usage from the predicate.";
            AddPrimaryDiagnostic(feedback);
            return ExpressionCompileResult.Failed(feedback);
        }

        var expressionSyntaxTree = CSharpSyntaxTree.ParseText(definition.Source, CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp12));
        var securityViolation = ExpressionSecurityWalker.FindViolation(expressionSyntaxTree.GetRoot());
        if (securityViolation is not null)
        {
            feedback.Status = "failed";
            feedback.DiagnosticCode = "LOOM.EXPR.SECURITY.UNAPPROVED_API";
            feedback.DiagnosticCategory = "security";
            feedback.Severity = "error";
            feedback.Message = securityViolation;
            feedback.SuggestedFix = "Use boolean operators, literals, and the read-only context.Get<T>(\"path\") or context.Has(\"path\") API only.";
            AddPrimaryDiagnostic(feedback);
            return ExpressionCompileResult.Failed(feedback);
        }

        var source = $$"""
            using System;
            public static class LoomExpressionHost
            {
                public static bool Evaluate(dynamic context) => {{definition.Source}};
            }
            """;
        var syntaxTree = CSharpSyntaxTree.ParseText(source, CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp12));
        var compilation = CSharpCompilation.Create(
            assemblyName: $"LoomExpression_{Guid.NewGuid():N}",
            syntaxTrees: [syntaxTree],
            references: GetReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release));

        using var assemblyStream = new MemoryStream();
        var emitResult = compilation.Emit(assemblyStream);
        var emitDiagnostics = emitResult.Diagnostics
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error
                || diagnostic.Severity == DiagnosticSeverity.Warning
                || diagnostic.Severity == DiagnosticSeverity.Info)
            .OrderBy(static diagnostic => diagnostic.Location.IsInSource ? diagnostic.Location.SourceSpan.Start : int.MaxValue)
            .ThenBy(static diagnostic => diagnostic.Id, StringComparer.Ordinal)
            .ToArray();
        feedback.DiagnosticCount = emitDiagnostics.Length;
        feedback.Truncated = emitDiagnostics.Length > MaxExpressionDiagnostics;
        feedback.Diagnostics = [.. emitDiagnostics.Take(MaxExpressionDiagnostics).Select(ToDiagnostic)];
        feedback.Warnings = [.. feedback.Diagnostics
            .Where(static diagnostic => string.Equals(diagnostic.Severity, "warning", StringComparison.OrdinalIgnoreCase))
            .Select(static diagnostic => diagnostic.Message)];
        if (!emitResult.Success)
        {
            var diagnostic = emitDiagnostics.FirstOrDefault(static item => item.Severity == DiagnosticSeverity.Error)
                ?? emitDiagnostics.FirstOrDefault();
            if (diagnostic is null)
            {
                feedback.Status = "failed";
                feedback.DiagnosticCode = "LOOM.EXPR.CSHARP.UNKNOWN";
                feedback.DiagnosticCategory = "syntax";
                feedback.Severity = "error";
                feedback.Message = "The C# predicate could not be compiled and produced no diagnostic.";
                feedback.SuggestedFix = "Correct the C# predicate and compile it again.";
                return ExpressionCompileResult.Failed(feedback);
            }
            feedback.Status = "failed";
            feedback.DiagnosticCode = $"LOOM.EXPR.CSHARP.{diagnostic.Id}";
            feedback.DiagnosticCategory = ToDiagnosticCategory(diagnostic);
            feedback.Severity = "error";
            feedback.Message = diagnostic.GetMessage();
            feedback.SuggestedFix = "Correct the C# predicate at the reported source span.";
            feedback.SourceSpan = ToSourceSpan(diagnostic);
            return ExpressionCompileResult.Failed(feedback);
        }

        assemblyStream.Position = 0;
        var assembly = AssemblyLoadContext.Default.LoadFromStream(assemblyStream);
        var method = assembly.GetType("LoomExpressionHost")?.GetMethod("Evaluate", BindingFlags.Public | BindingFlags.Static);
        if (method is null)
        {
            feedback.Status = "failed";
            feedback.DiagnosticCode = "LOOM.EXPR.RUNTIME.MISSING_ENTRY_POINT";
            feedback.DiagnosticCategory = "contract";
            feedback.Severity = "error";
            feedback.Message = "The compiled expression entry point could not be loaded.";
            AddPrimaryDiagnostic(feedback);
            return ExpressionCompileResult.Failed(feedback);
        }

        feedback.Status = "succeeded";
        feedback.DiagnosticCode = "LOOM.EXPR.OK";
        feedback.DiagnosticCategory = "semantic";
        feedback.Severity = "info";
        feedback.Message = "Expression compiled successfully.";
        feedback.Kind = definition.Kind;
        feedback.ResultType = definition.ResultType;
        feedback.Capabilities = [.. binding.RequiredExpressionCapabilities];
        return ExpressionCompileResult.Succeeded(feedback, context => (bool)(method.Invoke(null, [context]) ?? false));
    }

    private static void AddPrimaryDiagnostic(ExpressionCompileFeedback feedback)
    {
        feedback.Diagnostics = [new ExpressionCompileDiagnostic
        {
            Code = feedback.DiagnosticCode,
            Category = feedback.DiagnosticCategory,
            Severity = feedback.Severity,
            Message = feedback.Message,
            SourceSpan = feedback.SourceSpan,
            SuggestedFix = feedback.SuggestedFix,
        }];
        feedback.DiagnosticCount = 1;
        feedback.Truncated = false;
    }

    private static ExpressionCompileFeedback CreateFeedback(ExpressionBinding binding, ExpressionDefinition definition, string field)
    {
        return new ExpressionCompileFeedback
        {
            Language = binding.Language,
            LanguageVersion = binding.LanguageVersion,
            ContractId = binding.ContractId,
            ContractVersion = binding.ContractVersion,
            Field = field,
            CompilerIdentity = CompilerIdentity,
        };
    }

    private static IEnumerable<MetadataReference> GetReferences()
    {
        var trustedAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")
            ?? throw new InvalidOperationException("The .NET trusted platform assembly list is unavailable.");
        var requiredAssemblies = trustedAssemblies
            .Split(Path.PathSeparator)
            .Where(static path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
            .Distinct(StringComparer.Ordinal);

        return requiredAssemblies.Select(static path => MetadataReference.CreateFromFile(path));
    }

    private static ExpressionCompileDiagnostic ToDiagnostic(Diagnostic diagnostic)
    {
        return new ExpressionCompileDiagnostic
        {
            Code = $"LOOM.EXPR.CSHARP.{diagnostic.Id}",
            Category = ToDiagnosticCategory(diagnostic),
            Severity = ToDiagnosticSeverity(diagnostic.Severity),
            Message = Truncate(diagnostic.GetMessage(), 2048),
            SourceSpan = ToSourceSpan(diagnostic),
            SuggestedFix = "Correct the C# predicate at the reported source span."
        };
    }

    private static string ToDiagnosticCategory(Diagnostic diagnostic)
        => diagnostic.Id.StartsWith("CS", StringComparison.Ordinal) ? "semantic" : "syntax";

    private static string ToDiagnosticSeverity(DiagnosticSeverity severity)
        => severity switch
        {
            DiagnosticSeverity.Error => "error",
            DiagnosticSeverity.Warning => "warning",
            DiagnosticSeverity.Info => "info",
            _ => "hidden"
        };

    private static string Truncate(string value, int maximumLength)
        => value.Length <= maximumLength ? value : value[..maximumLength] + "...";

    private static ExpressionSourceSpan? ToSourceSpan(Diagnostic diagnostic)
    {
        if (!diagnostic.Location.IsInSource || diagnostic.Location.SourceTree is null)
        {
            return null;
        }

        var lineSpan = diagnostic.Location.GetLineSpan();
        return new ExpressionSourceSpan
        {
            StartLine = lineSpan.StartLinePosition.Line + 1,
            StartColumn = lineSpan.StartLinePosition.Character + 1,
            EndLine = lineSpan.EndLinePosition.Line + 1,
            EndColumn = lineSpan.EndLinePosition.Character + 1,
        };
    }
}

internal sealed class ExpressionSecurityWalker : CSharpSyntaxWalker
{
    private string? _violation;

    public static string? FindViolation(SyntaxNode node)
    {
        var walker = new ExpressionSecurityWalker();
        walker.Visit(node);
        return walker._violation;
    }

    public override void VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
    {
        Reject("Object creation is not allowed in workflow expressions.");
    }

    public override void VisitTypeOfExpression(TypeOfExpressionSyntax node)
    {
        Reject("Type inspection is not allowed in workflow expressions.");
    }

    public override void VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        if (_violation is null && !IsApprovedContextCall(node))
        {
            Reject("Only read-only context.Get<T>(\"path\") and context.Has(\"path\") calls are allowed in workflow expressions.");
            return;
        }

        base.VisitInvocationExpression(node);
    }

    private static bool IsApprovedContextCall(InvocationExpressionSyntax node)
    {
        var memberAccess = node.Expression switch
        {
            MemberAccessExpressionSyntax member => member,
            GenericNameSyntax => null,
            _ => null,
        };
        if (memberAccess?.Expression is not IdentifierNameSyntax { Identifier.ValueText: "context" })
        {
            return false;
        }

        var methodName = memberAccess.Name.Identifier.ValueText;
        return methodName is "Get" or "Has";
    }

    private void Reject(string message)
    {
        _violation ??= message;
    }
}

public sealed class ExpressionCompileResult
{
    private ExpressionCompileResult(ExpressionCompileFeedback feedback, Func<ExpressionRuntimeContext, bool>? execute)
    {
        Feedback = feedback;
        Execute = execute;
    }

    public ExpressionCompileFeedback Feedback { get; }

    public Func<ExpressionRuntimeContext, bool>? Execute { get; }

    public bool IsSuccess => Execute is not null;

    public static ExpressionCompileResult Succeeded(ExpressionCompileFeedback feedback, Func<ExpressionRuntimeContext, bool> execute)
        => new(feedback, execute);

    public static ExpressionCompileResult Failed(ExpressionCompileFeedback feedback)
        => new(feedback, null);
}
