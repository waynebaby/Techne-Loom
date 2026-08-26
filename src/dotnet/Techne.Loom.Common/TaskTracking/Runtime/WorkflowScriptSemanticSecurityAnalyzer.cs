using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Techne.Loom.Common.TaskTracking.Runtime;

internal static class WorkflowScriptSemanticSecurityAnalyzer
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

    private static readonly HashSet<string> ForbiddenTypeNames = new(StringComparer.Ordinal)
    {
        "File",
        "Directory",
        "FileInfo",
        "DirectoryInfo",
        "Process",
        "HttpClient",
        "Assembly",
        "AssemblyLoadContext",
        "Activator",
        "Marshal",
        "AppDomain",
        "Type",
        "MethodInfo",
        "PropertyInfo",
        "FieldInfo",
        "Delegate",
    };

    private static readonly HashSet<string> ForbiddenMemberNames = new(StringComparer.Ordinal)
    {
        "GetType",
        "GetMethod",
        "GetMethods",
        "GetProperty",
        "GetProperties",
        "GetField",
        "GetFields",
        "GetEvent",
        "GetEvents",
        "GetMember",
        "GetMembers",
        "GetAssemblies",
        "GetAssembly",
        "Invoke",
        "DynamicInvoke",
        "CreateInstance",
        "CreateDelegate",
        "Load",
        "LoadFrom",
        "LoadFile",
        "LoadModule",
        "CurrentDomain",
        "DefineDynamicAssembly",
        "RunClassConstructor",
    };

    public static string? FindViolation(CSharpCompilation compilation, SyntaxTree syntaxTree)
    {
        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        foreach (var node in syntaxTree.GetRoot().DescendantNodes())
        {
            var symbol = GetSymbol(semanticModel, node);
            if (symbol is null)
            {
                continue;
            }

            if (symbol is IMethodSymbol method && ForbiddenMemberNames.Contains(method.Name))
            {
                return $"The API or member '{method.Name}' is not allowed in workflow scripts.";
            }

            if (symbol is IPropertySymbol property && ForbiddenMemberNames.Contains(property.Name))
            {
                return $"The API or member '{property.Name}' is not allowed in workflow scripts.";
            }

            var containingType = GetContainingType(symbol);
            if (containingType is null)
            {
                continue;
            }

            var namespaceName = containingType.ContainingNamespace?.ToDisplayString() ?? string.Empty;
            if (ForbiddenNamespaces.Any(forbidden => namespaceName.Equals(forbidden, StringComparison.Ordinal) || namespaceName.StartsWith(forbidden + ".", StringComparison.Ordinal)))
            {
                return $"The API type '{containingType.ToDisplayString()}' is not allowed in workflow scripts.";
            }

            if (ForbiddenTypeNames.Contains(containingType.Name))
            {
                return $"The API type '{containingType.ToDisplayString()}' is not allowed in workflow scripts.";
            }
        }

        return null;
    }

    private static ISymbol? GetSymbol(SemanticModel semanticModel, SyntaxNode node)
    {
        return node switch
        {
            InvocationExpressionSyntax invocation => semanticModel.GetSymbolInfo(invocation).Symbol,
            ObjectCreationExpressionSyntax creation => semanticModel.GetSymbolInfo(creation).Symbol,
            MemberAccessExpressionSyntax memberAccess => semanticModel.GetSymbolInfo(memberAccess).Symbol,
            IdentifierNameSyntax identifier => semanticModel.GetSymbolInfo(identifier).Symbol,
            _ => null,
        };
    }

    private static INamedTypeSymbol? GetContainingType(ISymbol symbol)
    {
        return symbol switch
        {
            INamedTypeSymbol type => type,
            IMethodSymbol method => method.ContainingType,
            IPropertySymbol property => property.ContainingType,
            IFieldSymbol field => field.ContainingType,
            IEventSymbol @event => @event.ContainingType,
            _ => null,
        };
    }
}
