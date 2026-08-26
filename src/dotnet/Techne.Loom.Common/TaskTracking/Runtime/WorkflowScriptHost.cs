using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public sealed class WorkflowScriptHostOptions
{
    public TimeSpan ExecutionTimeout { get; init; } = TimeSpan.FromSeconds(30);
}

public sealed class WorkflowScriptHost
{
    private const string CompilerIdentity = "Microsoft.CodeAnalysis.CSharp";
    private static readonly string[] AllowedUsingNamespaces =
    [
        "System",
        "System.Collections.Generic",
        "System.Linq",
        "Techne.Loom.Abstractions.TaskTracking.Model",
    ];

    public Task<WorkflowScriptExecution<WorkflowInstance>> ExecuteBuilderAsync(
        string scriptFile,
        WorkflowScriptInput input,
        WorkflowScriptHostOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        return ExecuteAsync<WorkflowInstance>(
            scriptFile,
            "Build",
            [typeof(WorkflowScriptInput)],
            [input],
            typeof(WorkflowInstance),
            options,
            cancellationToken);
    }

    public Task<WorkflowScriptExecution<WorkflowInstance>> ExecuteEditorAsync(
        string scriptFile,
        WorkflowInstance workflow,
        WorkflowScriptInput input,
        WorkflowScriptHostOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(input);
        return ExecuteAsync<WorkflowInstance>(
            scriptFile,
            "Edit",
            [typeof(WorkflowInstance), typeof(WorkflowScriptInput)],
            [workflow, input],
            typeof(WorkflowInstance),
            options,
            cancellationToken);
    }
    public Task<WorkflowScriptExecution<WorkflowScriptVerificationResult>> ExecuteVerifierAsync(
        string scriptFile,
        WorkflowInstance actual,
        WorkflowInstance reference,
        WorkflowModelReference model,
        WorkflowScriptHostOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actual);
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(model);
        return ExecuteAsync<WorkflowScriptVerificationResult>(
            scriptFile,
            "Verify",
            [typeof(WorkflowInstance), typeof(WorkflowInstance), typeof(WorkflowModelReference)],
            [actual, reference, model],
            typeof(WorkflowScriptVerificationResult),
            options,
            cancellationToken);
    }

    private static async Task<WorkflowScriptExecution<T>> ExecuteAsync<T>(
        string scriptFile,
        string entryPoint,
        IReadOnlyList<Type> parameterTypes,
        IReadOnlyList<object?> arguments,
        Type returnType,
        WorkflowScriptHostOptions? options,
        CancellationToken cancellationToken)
    {
        var feedback = new WorkflowScriptExecutionFeedback
        {
            ScriptFile = string.IsNullOrWhiteSpace(scriptFile) ? string.Empty : Path.GetFullPath(scriptFile),
            EntryPoint = entryPoint,
            CompilerIdentity = CompilerIdentity,
        };

        if (string.IsNullOrWhiteSpace(scriptFile))
        {
            return Failure<T>(feedback, "LOOM.SCRIPT.INPUT.MISSING_FILE", "contract", "A script file is required.", "Provide an existing C# workflow script file.");
        }

        var fullScriptFile = Path.GetFullPath(scriptFile);
        if (!File.Exists(fullScriptFile))
        {
            return Failure<T>(feedback, "LOOM.SCRIPT.INPUT.FILE_NOT_FOUND", "contract", $"The script file '{fullScriptFile}' does not exist.", "Provide an existing C# workflow script file.");
        }

        string source;
        try
        {
            source = await File.ReadAllTextAsync(fullScriptFile, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Failure<T>(feedback, "LOOM.SCRIPT.INPUT.READ_FAILED", "contract", exception.Message, "Make the script file readable and run the command again.");
        }

        var syntaxTree = CSharpSyntaxTree.ParseText(
            AddAllowedUsings(source),
            CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp12),
            path: fullScriptFile);
        var securityViolation = WorkflowScriptSecurityWalker.FindViolation(syntaxTree.GetRoot());
        if (securityViolation is not null)
        {
            return Failure<T>(feedback, "LOOM.SCRIPT.SECURITY.UNAPPROVED_API", "security", securityViolation, "Use only the workflow model facade, basic collections, and synchronous pure computation.");
        }

        var compilation = CSharpCompilation.Create(
            assemblyName: $"LoomWorkflowScript_{Guid.NewGuid():N}",
            syntaxTrees: [syntaxTree],
            references: GetReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release));
        var semanticSecurityViolation = WorkflowScriptSemanticSecurityAnalyzer.FindViolation(compilation, syntaxTree);
        if (semanticSecurityViolation is not null)
        {
            return Failure<T>(feedback, "LOOM.SCRIPT.SECURITY.UNAPPROVED_API", "security", semanticSecurityViolation, "Use only the workflow model facade, basic collections, and synchronous pure computation.");
        }

        using var assemblyStream = new MemoryStream();
        var emitResult = compilation.Emit(assemblyStream);
        var diagnostics = emitResult.Diagnostics
            .Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .Select(ToDiagnostic)
            .ToList();
        feedback.Diagnostics = diagnostics;

        if (!emitResult.Success)
        {
            var firstError = emitResult.Diagnostics.FirstOrDefault(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
            feedback.DiagnosticCode = firstError?.Id ?? "LOOM.SCRIPT.COMPILE.FAILED";
            feedback.DiagnosticCategory = firstError?.Id.StartsWith("CS", StringComparison.Ordinal) == true ? "semantic" : "syntax";
            feedback.Error = firstError?.GetMessage() ?? "The workflow script did not compile.";
            feedback.SuggestedFix = "Correct the C# script at the reported source span and run it again.";
            return Failure<T>(feedback, feedback.DiagnosticCode, feedback.DiagnosticCategory, feedback.Error, feedback.SuggestedFix);
        }

        try
        {
            assemblyStream.Position = 0;
            var assembly = AssemblyLoadContext.Default.LoadFromStream(assemblyStream);
            var method = FindEntryPoint(assembly, entryPoint, parameterTypes, returnType);
            if (method is null)
            {
                return Failure<T>(feedback, "LOOM.SCRIPT.CONTRACT.MISSING_ENTRY_POINT", "contract", $"The script must expose a public static {entryPoint} method with the expected parameters and return type.", $"Add public static {entryPoint}(... ) returning {returnType.Name}.");
            }

            var timeout = options?.ExecutionTimeout ?? TimeSpan.FromSeconds(30);
            if (timeout <= TimeSpan.Zero)
            {
                return Failure<T>(feedback, "LOOM.SCRIPT.INPUT.INVALID_TIMEOUT", "contract", "The script execution timeout must be positive.", "Set a positive execution timeout.");
            }

            var invocationTask = Task.Run(() => method.Invoke(null, arguments.ToArray()), cancellationToken);
            object? rawResult;
            try
            {
                rawResult = await invocationTask.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                return Failure<T>(feedback, "LOOM.SCRIPT.RUNTIME.TIMEOUT", "resource", "The workflow script exceeded its synchronous execution timeout.", "Reduce the script work or increase the explicit timeout. The in-process timeout is not a security sandbox.");
            }

            if (rawResult is not T result)
            {
                return Failure<T>(feedback, "LOOM.SCRIPT.CONTRACT.INVALID_RETURN", "contract", $"The {entryPoint} method returned '{rawResult?.GetType().FullName ?? "null"}', expected '{returnType.FullName}'.", $"Return a {returnType.Name} instance from {entryPoint}.");
            }

            feedback.Status = "succeeded";
            feedback.DiagnosticCode = "LOOM.SCRIPT.OK";
            feedback.DiagnosticCategory = "semantic";
            feedback.Error = null;
            feedback.SuggestedFix = null;
            return new WorkflowScriptExecution<T>
            {
                Value = result,
                Feedback = feedback,
            };
        }
        catch (TargetInvocationException exception)
        {
            var cause = exception.InnerException ?? exception;
            return Failure<T>(feedback, "LOOM.SCRIPT.RUNTIME.EXCEPTION", "runtime", cause.Message, "Fix the script's runtime exception and run it again.");
        }
        catch (Exception exception) when (exception is BadImageFormatException or FileLoadException or FileNotFoundException or InvalidOperationException)
        {
            return Failure<T>(feedback, "LOOM.SCRIPT.RUNTIME.LOAD_FAILED", "runtime", exception.Message, "Use the model reference generated by the same runtime and run the script again.");
        }
    }

    private static string AddAllowedUsings(string source)
    {
        var prefix = string.Join(Environment.NewLine, AllowedUsingNamespaces.Select(static item => $"using {item};"));
        return prefix + Environment.NewLine + "#nullable enable" + Environment.NewLine + source;
    }

    private static IEnumerable<MetadataReference> GetReferences()
    {
        var paths = new[]
        {
            typeof(object).Assembly.Location,
            typeof(WorkflowInstance).Assembly.Location,
        };
        var trustedAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")
            ?? throw new InvalidOperationException("The .NET trusted platform assembly list is unavailable.");
        var allowedPlatformNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "System.Private.CoreLib.dll",
            "System.Runtime.dll",
            "System.Collections.dll",
            "System.Linq.dll",
            "System.ObjectModel.dll",
            "System.Runtime.Extensions.dll",
        };
        var platformPaths = trustedAssemblies
            .Split(Path.PathSeparator)
            .Where(path => allowedPlatformNames.Contains(Path.GetFileName(path)));

        return paths
            .Concat(platformPaths)
            .Where(static path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
            .Distinct(StringComparer.Ordinal)
            .Select(static path => MetadataReference.CreateFromFile(path));
    }

    private static MethodInfo? FindEntryPoint(
        Assembly assembly,
        string name,
        IReadOnlyList<Type> parameterTypes,
        Type returnType)
    {
        return assembly.GetTypes()
            .SelectMany(static type => type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Where(method => string.Equals(method.Name, name, StringComparison.Ordinal))
            .Where(static method => !method.IsGenericMethodDefinition)
            .Where(method => method.ReturnType == returnType)
            .SingleOrDefault(method => method.GetParameters()
                .Select(static parameter => parameter.ParameterType)
                .SequenceEqual(parameterTypes));
    }

    private static WorkflowScriptDiagnostic ToDiagnostic(Diagnostic diagnostic)
    {
        var item = new WorkflowScriptDiagnostic
        {
            Id = diagnostic.Id,
            Severity = diagnostic.Severity.ToString().ToLowerInvariant(),
            Message = diagnostic.GetMessage(),
        };
        if (diagnostic.Location.IsInSource && diagnostic.Location.SourceTree is not null)
        {
            var span = diagnostic.Location.GetLineSpan();
            item.StartLine = span.StartLinePosition.Line + 1;
            item.StartColumn = span.StartLinePosition.Character + 1;
            item.EndLine = span.EndLinePosition.Line + 1;
            item.EndColumn = span.EndLinePosition.Character + 1;
        }

        return item;
    }

    private static WorkflowScriptExecution<T> Failure<T>(
        WorkflowScriptExecutionFeedback feedback,
        string code,
        string category,
        string error,
        string suggestedFix)
    {
        feedback.Status = "failed";
        feedback.DiagnosticCode = code;
        feedback.DiagnosticCategory = category;
        feedback.Error = error;
        feedback.SuggestedFix = suggestedFix;
        return new WorkflowScriptExecution<T>
        {
            Feedback = feedback,
        };
    }
}

internal sealed class WorkflowScriptSecurityWalker : CSharpSyntaxWalker
{
    private static readonly string[] ForbiddenNamespaces =
    [
        "System.IO",
        "System.Net",
        "System.Diagnostics",
        "System.Reflection",
        "System.Runtime.Loader",
        "System.Threading",
    ];

    private static readonly HashSet<string> ForbiddenIdentifiers = new(StringComparer.Ordinal)
    {
        "File",
        "Directory",
        "Process",
        "HttpClient",
        "Assembly",
        "AssemblyLoadContext",
        "Activator",
        "Marshal",
        "Reflection",
        "Thread",
        "Task",
        "Environment",
        "DllImport",
    };

    private string? _violation;

    public static string? FindViolation(SyntaxNode root)
    {
        var walker = new WorkflowScriptSecurityWalker();
        walker.Visit(root);
        if (walker._violation is null && root.DescendantTokens().Any(static token => token.IsKind(SyntaxKind.AsyncKeyword) || token.IsKind(SyntaxKind.AwaitKeyword) || token.IsKind(SyntaxKind.UnsafeKeyword)))
        {
            walker._violation = "async, await, and unsafe code are not allowed in workflow scripts.";
        }

        return walker._violation;
    }

    public override void VisitUsingDirective(UsingDirectiveSyntax node)
    {
        var namespaceName = node.Name?.ToString() ?? string.Empty;
        if (_violation is null && ForbiddenNamespaces.Any(namespaceName.StartsWith))
        {
            _violation = $"The namespace '{namespaceName}' is not allowed in workflow scripts.";
        }

        base.VisitUsingDirective(node);
    }

    public override void VisitIdentifierName(IdentifierNameSyntax node)

    {

        if (_violation is null

            && ForbiddenIdentifiers.Contains(node.Identifier.ValueText)

            && !IsAllowedModelMemberName(node))

        {

            _violation = $"The API or identifier '{node.Identifier.ValueText}' is not allowed in workflow scripts.";

        }



        base.VisitIdentifierName(node);

    }



    private static bool IsAllowedModelMemberName(IdentifierNameSyntax node)
    {
        return node.Parent switch
        {
            MemberAccessExpressionSyntax memberAccess when ReferenceEquals(memberAccess.Name, node) => true,
            AssignmentExpressionSyntax assignment when ReferenceEquals(assignment.Left, node) && assignment.Parent is InitializerExpressionSyntax => true,
            _ => false,
        };
    }

    public override void VisitTypeOfExpression(TypeOfExpressionSyntax node)
    {
        _violation ??= "Type inspection is not allowed in workflow scripts.";
        base.VisitTypeOfExpression(node);
    }

    public override void VisitAttribute(AttributeSyntax node)
    {
        if (_violation is null && node.Name.ToString().Contains("DllImport", StringComparison.Ordinal))
        {
            _violation = "Native library imports are not allowed in workflow scripts.";
        }

        base.VisitAttribute(node);
    }
}
