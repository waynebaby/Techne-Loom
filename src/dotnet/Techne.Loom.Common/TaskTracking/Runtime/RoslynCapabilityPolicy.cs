using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Techne.Loom.Common.TaskTracking.Runtime;

internal sealed record RoslynCapabilityViolation(
    string Code,
    string Message,
    string SuggestedFix,
    string? CapabilityId = null);

internal static class RoslynCapabilityPolicy
{
    private const double MaxRegexTimeoutSeconds = 5;
    private const int MaxCollectionItems = 32;
    private const int MaxCollectionProjectedBytes = 32 * 1024;
    private const int MaxCollectionDepth = 6;

    private static readonly HashSet<string> BaselineExpressionCapabilities =
    [
        RoslynCapabilityCatalog.ExpressionContextGet,
        RoslynCapabilityCatalog.ExpressionContextHas,
        RoslynCapabilityCatalog.ExpressionContextIndexer,
    ];

    private static readonly HashSet<string> ForbiddenScriptNamespaces =
    [
        "System.IO",
        "System.Net",
        "System.Diagnostics",
        "System.Reflection",
        "System.Runtime.Loader",
        "System.Threading",
        "System.Runtime.InteropServices",
    ];

    private static readonly HashSet<string> ForbiddenScriptTypeNames =
    [
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
        "Environment",
        "Thread",
        "Task",
        "Random",
        "RandomNumberGenerator",
        "RNGCryptoServiceProvider",
    ];

    private static readonly HashSet<string> ForbiddenScriptMemberNames =
    [
        "GetType",
        "GetMethod",
        "GetMethods",
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
        "Now",
        "UtcNow",
        "Today",
        "NewGuid",
    ];

    public static RoslynCapabilityViolation? ValidateDeclaredExpressionCapabilities(IEnumerable<string>? declaredCapabilities)
    {
        if (declaredCapabilities is null)
        {
            return null;
        }

        foreach (var capabilityId in declaredCapabilities)
        {
            if (string.IsNullOrWhiteSpace(capabilityId)
                || !RoslynCapabilityCatalog.TryGet(capabilityId, out var descriptor))
            {
                return new RoslynCapabilityViolation(
                    "LOOM.EXPR.CONTRACT.UNKNOWN_CAPABILITY",
                    $"The expression capability '{capabilityId}' is not known to this runtime.",
                    "Remove the unknown capability or use a capability id listed by the current runtime.");
            }

            if (descriptor.Surface != RoslynCapabilitySurface.Expression)
            {
                return new RoslynCapabilityViolation(
                    "LOOM.EXPR.CONTRACT.SURFACE_MISMATCH",
                    $"The capability '{capabilityId}' is available to workflow scripts, not predicate expressions.",
                    "Declare an expression capability that is supported on the current expression surface.",
                    capabilityId);
            }
        }

        return null;
    }

    public static RoslynCapabilityViolation? ValidateExpression(
        SemanticModel semanticModel,
        ExpressionSyntax expression,
        IReadOnlySet<string> declaredCapabilities,
        ISet<string> resolvedCapabilities,
        ISet<string> referencedSymbols)
    {
        foreach (var node in expression.DescendantNodesAndSelf())
        {
            if (node is AssignmentExpressionSyntax)
            {
                return new RoslynCapabilityViolation(
                    "LOOM.EXPR.SECURITY.MUTATION",
                    "Assignments are not allowed in workflow expressions.",
                    "Use read-only context values and boolean operators instead.");
            }

            if (node is PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.PreIncrementExpression or (int)SyntaxKind.PreDecrementExpression }
                || node is PostfixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.PostIncrementExpression or (int)SyntaxKind.PostDecrementExpression })
            {
                return new RoslynCapabilityViolation(
                    "LOOM.EXPR.SECURITY.MUTATION",
                    "Increment and decrement operations are not allowed in workflow expressions.",
                    "Compute a value without changing state.");
            }

            if (node is BinaryExpressionSyntax binary
                && semanticModel.GetSymbolInfo(binary).Symbol is IMethodSymbol { MethodKind: MethodKind.UserDefinedOperator } userDefinedOperator)
            {
                return new RoslynCapabilityViolation(
                    "LOOM.EXPR.SECURITY.UNAPPROVED_API",
                    $"The operator '{userDefinedOperator.Name}' is not approved for workflow expressions.",
                    "Use built-in boolean, comparison, arithmetic, and coalescing operators only.");
            }

            if (node is PrefixUnaryExpressionSyntax or PostfixUnaryExpressionSyntax
                && semanticModel.GetSymbolInfo(node).Symbol is IMethodSymbol { MethodKind: MethodKind.UserDefinedOperator } unaryOperator)
            {
                return new RoslynCapabilityViolation(
                    "LOOM.EXPR.SECURITY.UNAPPROVED_API",
                    $"The operator '{unaryOperator.Name}' is not approved for workflow expressions.",
                    "Use built-in boolean, comparison, arithmetic, and coalescing operators only.");
            }

            if (node is CastExpressionSyntax cast)
            {
                var targetType = semanticModel.GetTypeInfo(cast.Type).Type;
                if (targetType is not null && semanticModel.ClassifyConversion(cast.Expression, targetType).IsUserDefined)
                {
                    return new RoslynCapabilityViolation(
                        "LOOM.EXPR.SECURITY.UNAPPROVED_API",
                        "User-defined conversions are not allowed in workflow expressions.",
                        "Use a built-in conversion or read a value with the required context.Get<T> type.");
                }
            }

            if (node is InvocationExpressionSyntax invocation)
            {
                var symbol = semanticModel.GetSymbolInfo(invocation).Symbol;
                if (symbol is not IMethodSymbol method)
                {
                    continue;
                }

                referencedSymbols.Add(method.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                var descriptor = FindCapability(method, RoslynCapabilitySurface.Expression);
                if (descriptor is null)
                {
                    return UnapprovedExpressionSymbol(method);
                }
                if (method.ContainingType.ToDisplayString() == "Techne.Loom.Common.TaskTracking.Runtime.ExpressionRuntimeContext"
                    && method.Name == "Get"
                    && method.TypeArguments.Length == 1
                    && !IsAllowedExpressionType(method.TypeArguments[0]))
                {
                    return new RoslynCapabilityViolation(
                        "LOOM.EXPR.SECURITY.UNAPPROVED_TYPE",
                        $"The context value type '{method.TypeArguments[0].ToDisplayString()}' is not allowed in workflow expressions.",
                        "Use a primitive, string, or bounded primitive/string collection type with context.Get<T>.");
                }


                resolvedCapabilities.Add(descriptor.Id);
                if (!BaselineExpressionCapabilities.Contains(descriptor.Id) && !declaredCapabilities.Contains(descriptor.Id))
                {
                    return MissingDeclaration(descriptor);
                }

                var constraintViolation = ValidateExpressionConstraint(semanticModel, invocation, method, descriptor);
                if (constraintViolation is not null)
                {
                    return constraintViolation;
                }

                continue;
            }

            if (node is ElementAccessExpressionSyntax elementAccess)
            {
                var symbol = semanticModel.GetSymbolInfo(elementAccess).Symbol;
                if (symbol is IPropertySymbol property)
                {
                    referencedSymbols.Add(property.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                    var descriptor = FindCapability(property, RoslynCapabilitySurface.Expression);
                    if (descriptor is null)
                    {
                        return UnapprovedExpressionSymbol(property);
                    }

                    resolvedCapabilities.Add(descriptor.Id);
                    if (!BaselineExpressionCapabilities.Contains(descriptor.Id) && !declaredCapabilities.Contains(descriptor.Id))
                    {
                        return MissingDeclaration(descriptor);
                    }
                }

                continue;
            }

            if (node is MemberAccessExpressionSyntax memberAccess)
            {
                var symbol = semanticModel.GetSymbolInfo(memberAccess).Symbol;
                if (symbol is IPropertySymbol or IFieldSymbol)
                {
                    referencedSymbols.Add(symbol.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                    var descriptor = FindCapability(symbol, RoslynCapabilitySurface.Expression);
                    if (descriptor is null)
                    {
                        return UnapprovedExpressionSymbol(symbol);
                    }

                    resolvedCapabilities.Add(descriptor.Id);
                    if (!BaselineExpressionCapabilities.Contains(descriptor.Id) && !declaredCapabilities.Contains(descriptor.Id))
                    {
                        return MissingDeclaration(descriptor);
                    }
                }
            }

            if (node is AnonymousFunctionExpressionSyntax lambda && !IsAuthorizedCollectionLambda(semanticModel, lambda))
            {
                return new RoslynCapabilityViolation(
                    "LOOM.EXPR.SECURITY.DELEGATE",
                    "Delegate and lambda expressions are not allowed in workflow expressions except as predicates for bounded Any or All operations.",
                    "Use a scalar predicate or apply Any/All to a bounded context collection.");
            }
        }

        return null;
    }

    public static string? FindScriptViolation(CSharpCompilation compilation, SyntaxTree syntaxTree)
    {
        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        foreach (var node in syntaxTree.GetRoot().DescendantNodes())
        {
            if (node is ObjectCreationExpressionSyntax creation
                && semanticModel.GetSymbolInfo(creation).Symbol is IMethodSymbol constructor
                && constructor.ContainingType.ToDisplayString() == "System.Text.RegularExpressions.Regex")
            {
                return "Regex instances are not allowed in workflow scripts; use an approved static overload with an explicit timeout.";
            }

            var symbol = GetSymbol(semanticModel, node);
            if (symbol is null)
            {
                continue;
            }

            if (symbol is IAliasSymbol alias)
            {
                symbol = alias.Target;
            }

            if (symbol.Locations.Any(static location => location.IsInSource))
            {
                continue;
            }

            if (IsForbiddenScriptSymbol(symbol))
            {
                var containingType = GetContainingType(symbol);
                return containingType is not null && IsForbiddenScriptType(containingType)
                    ? $"The API type '{containingType.ToDisplayString()}' is not allowed in workflow scripts."
                    : $"The API or member '{symbol.Name}' is not allowed in workflow scripts.";
            }

            if (IsBuiltInScriptSymbol(symbol))
            {
                continue;
            }

            var descriptor = FindCapability(symbol, RoslynCapabilitySurface.Script);
            if (descriptor is not null)
            {
                var constraintViolation = node is InvocationExpressionSyntax invocation && symbol is IMethodSymbol method
                    ? ValidateScriptConstraint(semanticModel, invocation, method, descriptor)
                    : null;
                if (constraintViolation is not null)
                {
                    return constraintViolation.Message;
                }

                continue;
            }

            if (symbol is INamespaceSymbol or IAssemblySymbol)
            {
                continue;
            }

            if (symbol is ITypeParameterSymbol or IParameterSymbol or ILocalSymbol or IRangeVariableSymbol)
            {
                continue;
            }

            return $"The API symbol '{symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}' is not allowed in workflow scripts.";
        }

        return null;
    }

    private static RoslynCapabilityViolation? ValidateExpressionConstraint(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        RoslynCapabilityDescriptor descriptor)
    {
        return descriptor.Constraint switch
        {
            RoslynCapabilityConstraint.OrdinalComparison => ValidateOrdinalComparison(semanticModel, invocation),
            RoslynCapabilityConstraint.ConstantTimeSpan => ValidateTimeSpanFactory(semanticModel, invocation, method),
            RoslynCapabilityConstraint.RegexMatchTimeout => ValidateRegexInvocation(semanticModel, invocation, method),
            RoslynCapabilityConstraint.InvariantParsing => ValidateInvariantParsing(semanticModel, invocation, method),
            RoslynCapabilityConstraint.BoundedCollection => ValidateBoundedCollection(semanticModel, invocation, method),
            _ => null,
        };
    }

    private static RoslynCapabilityViolation? ValidateScriptConstraint(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        RoslynCapabilityDescriptor descriptor)
    {
        return descriptor.Constraint switch
        {
            RoslynCapabilityConstraint.RegexMatchTimeout => ValidateRegexInvocation(semanticModel, invocation, method),
            RoslynCapabilityConstraint.InvariantParsing => ValidateInvariantParsing(semanticModel, invocation, method),
            RoslynCapabilityConstraint.InMemoryHashing when descriptor.Id == RoslynCapabilityCatalog.ScriptEncoding => ValidateUtf8Encoding(semanticModel, invocation, method),
            _ => null,
        };
    }

    private static RoslynCapabilityViolation? ValidateUtf8Encoding(SemanticModel semanticModel, InvocationExpressionSyntax invocation, IMethodSymbol method)
    {
        if (method.Name is not (nameof(Encoding.GetBytes) or nameof(Encoding.GetString)))
        {
            return null;
        }

        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess
            && memberAccess.Expression is MemberAccessExpressionSyntax receiver
            && semanticModel.GetSymbolInfo(receiver).Symbol is IPropertySymbol property
            && string.Equals(property.GetDocumentationCommentId(), "P:System.Text.Encoding.UTF8", StringComparison.Ordinal))
        {
            return null;
        }

        return new RoslynCapabilityViolation(
            "LOOM.SCRIPT.SECURITY.UTF8_REQUIRED",
            "Encoding operations are allowed only through Encoding.UTF8.",
            "Use Encoding.UTF8.GetBytes(string) or Encoding.UTF8.GetString(byte[]).");
    }

    private static RoslynCapabilityViolation? ValidateOrdinalComparison(SemanticModel semanticModel, InvocationExpressionSyntax invocation)
    {
        var comparisonArgument = invocation.ArgumentList.Arguments
            .Select(argument => argument.Expression)
            .FirstOrDefault(expression => semanticModel.GetTypeInfo(expression).Type?.ToDisplayString() == "System.StringComparison");
        if (comparisonArgument is null || !semanticModel.GetConstantValue(comparisonArgument).HasValue)
        {
            return new RoslynCapabilityViolation(
                "LOOM.EXPR.SECURITY.STRING_COMPARISON_REQUIRED",
                "String comparison must use a compile-time StringComparison.Ordinal or StringComparison.OrdinalIgnoreCase value.",
                "Pass StringComparison.Ordinal or StringComparison.OrdinalIgnoreCase explicitly.");
        }

        var value = Convert.ToInt32(semanticModel.GetConstantValue(comparisonArgument).Value, CultureInfo.InvariantCulture);
        if (value is not (int)StringComparison.Ordinal and not (int)StringComparison.OrdinalIgnoreCase)
        {
            return new RoslynCapabilityViolation(
                "LOOM.EXPR.SECURITY.STRING_COMPARISON_REQUIRED",
                "Only ordinal string comparisons are allowed in workflow expressions.",
                "Use StringComparison.Ordinal or StringComparison.OrdinalIgnoreCase.");
        }

        return null;
    }

    private static RoslynCapabilityViolation? ValidateTimeSpanFactory(SemanticModel semanticModel, InvocationExpressionSyntax invocation, IMethodSymbol method)
    {
        if (invocation.ArgumentList.Arguments.Count != 1)
        {
            return new RoslynCapabilityViolation(
                "LOOM.EXPR.SECURITY.TIMEOUT_REQUIRED",
                "Timeout factories must receive one compile-time numeric value.",
                "Use TimeSpan.FromSeconds(value) or TimeSpan.FromMilliseconds(value) with a numeric constant.");
        }

        var constant = semanticModel.GetConstantValue(invocation.ArgumentList.Arguments[0].Expression);
        if (!constant.HasValue || !TryGetFiniteDouble(constant.Value, out var numericValue))
        {
            return new RoslynCapabilityViolation(
                "LOOM.EXPR.SECURITY.TIMEOUT_REQUIRED",
                "Regex timeout values must be finite compile-time numeric constants.",
                "Use a positive numeric literal or constant in TimeSpan.FromSeconds or FromMilliseconds.");
        }

        var seconds = string.Equals(method.Name, nameof(TimeSpan.FromMilliseconds), StringComparison.Ordinal)
            ? numericValue / 1000
            : numericValue;
        if (seconds <= 0 || seconds > MaxRegexTimeoutSeconds)
        {
            return new RoslynCapabilityViolation(
                "LOOM.EXPR.SECURITY.TIMEOUT_RANGE",
                "Regex timeout must be greater than zero and no greater than five seconds.",
                "Use a finite timeout between 1 millisecond and 5 seconds.");
        }

        return null;
    }

    private static RoslynCapabilityViolation? ValidateRegexInvocation(SemanticModel semanticModel, InvocationExpressionSyntax invocation, IMethodSymbol method)
    {
        if (method.Name is nameof(Regex.Escape) or nameof(Regex.Unescape))
        {
            return null;
        }

        var optionsIndex = method.Name == nameof(Regex.Replace) ? 3 : 2;
        var timeoutIndex = optionsIndex + 1;
        if (invocation.ArgumentList.Arguments.Count != timeoutIndex + 1)
        {
            return new RoslynCapabilityViolation(
                "LOOM.EXPR.SECURITY.REGEX_TIMEOUT_REQUIRED",
                "Regex matching requires the overload that includes RegexOptions and TimeSpan.",
                "Use the static Regex overload with options and an explicit finite timeout.");
        }

        var options = invocation.ArgumentList.Arguments[optionsIndex].Expression;
        var optionsViolation = ValidateRegexOptions(semanticModel, options);
        if (optionsViolation is not null)
        {
            return optionsViolation;
        }

        var timeout = invocation.ArgumentList.Arguments[timeoutIndex].Expression;
        if (timeout is not InvocationExpressionSyntax timeoutInvocation
            || semanticModel.GetSymbolInfo(timeoutInvocation).Symbol is not IMethodSymbol timeoutMethod
            || timeoutMethod.ContainingType.ToDisplayString() != "System.TimeSpan"
            || timeoutMethod.Name is not (nameof(TimeSpan.FromSeconds) or nameof(TimeSpan.FromMilliseconds)))
        {
            return new RoslynCapabilityViolation(
                "LOOM.EXPR.SECURITY.REGEX_TIMEOUT_REQUIRED",
                "Regex matching requires a finite TimeSpan created by FromSeconds or FromMilliseconds.",
                "Pass TimeSpan.FromSeconds(value) or TimeSpan.FromMilliseconds(value) using a compile-time constant.");
        }

        return ValidateTimeSpanFactory(semanticModel, timeoutInvocation, timeoutMethod);
    }

    private static RoslynCapabilityViolation? ValidateScriptRegexMethod(SemanticModel semanticModel, IMethodSymbol method)
    {
        if (method.Name is nameof(Regex.Escape) or nameof(Regex.Unescape))
        {
            return null;
        }

        return new RoslynCapabilityViolation(
            "LOOM.SCRIPT.SECURITY.REGEX_TIMEOUT_REQUIRED",
            "Regex matching requires the overload that includes RegexOptions and a finite TimeSpan timeout.",
            "Use the static Regex overload with options and a timeout no greater than five seconds.");
    }

    private static RoslynCapabilityViolation? ValidateRegexOptions(SemanticModel semanticModel, ExpressionSyntax expression)
    {
        if (!semanticModel.GetConstantValue(expression).HasValue)
        {
            return new RoslynCapabilityViolation(
                "LOOM.EXPR.SECURITY.REGEX_OPTIONS_REQUIRED",
                "RegexOptions must be a compile-time combination of approved option values.",
                "Use RegexOptions.None, CultureInvariant, IgnoreCase, Multiline, Singleline, ExplicitCapture, IgnorePatternWhitespace, or NonBacktracking.");
        }

        var value = Convert.ToInt32(semanticModel.GetConstantValue(expression).Value, CultureInfo.InvariantCulture);
        var allowed = (int)(RegexOptions.CultureInvariant
            | RegexOptions.IgnoreCase
            | RegexOptions.Multiline
            | RegexOptions.Singleline
            | RegexOptions.ExplicitCapture
            | RegexOptions.IgnorePatternWhitespace
            | RegexOptions.NonBacktracking);
        if ((value & ~allowed) != 0)
        {
            return new RoslynCapabilityViolation(
                "LOOM.EXPR.SECURITY.REGEX_OPTIONS_REQUIRED",
                "The RegexOptions combination contains a disallowed option.",
                "Remove Compiled, RightToLeft, ECMAScript, and any other option outside the common allowlist.");
        }

        return null;
    }

    private static RoslynCapabilityViolation? ValidateInvariantParsing(SemanticModel semanticModel, InvocationExpressionSyntax invocation, IMethodSymbol method)
    {
        var containingType = method.ContainingType.ToDisplayString();
        if (containingType is "System.DateTimeOffset" or "System.TimeSpan")
        {
            if (invocation.ArgumentList.Arguments.Count != 5
                || !IsCompileTimeString(semanticModel, invocation.ArgumentList.Arguments[1].Expression)
                || !IsInvariantCulture(semanticModel, invocation.ArgumentList.Arguments[2].Expression)
                || !IsAllowedEnumConstant(semanticModel, invocation.ArgumentList.Arguments[3].Expression, containingType == "System.DateTimeOffset" ? "System.Globalization.DateTimeStyles" : "System.Globalization.TimeSpanStyles"))
            {
                return new RoslynCapabilityViolation(
                    "LOOM.EXPR.SECURITY.INVARIANT_PARSE_REQUIRED",
                    "Exact date and duration parsing requires a compile-time format, CultureInfo.InvariantCulture, and an approved style value.",
                    "Use the explicit TryParseExact overload with a constant format, invariant culture, and an allowed style.");
            }

            return null;
        }

        if (containingType == "System.Guid")
        {
            if (invocation.ArgumentList.Arguments.Count == 2)
            {
                return null;
            }

            if (invocation.ArgumentList.Arguments.Count != 3 || !IsInvariantCulture(semanticModel, invocation.ArgumentList.Arguments[1].Expression))
            {
                return new RoslynCapabilityViolation(
                    "LOOM.EXPR.SECURITY.INVARIANT_PARSE_REQUIRED",
                    "Guid parsing must use the explicit string overload or CultureInfo.InvariantCulture.",
                    "Use Guid.TryParse(text, out value) or pass CultureInfo.InvariantCulture explicitly.");
            }

            return null;
        }

        if (invocation.ArgumentList.Arguments.Count != 4
            || !IsAllowedEnumConstant(semanticModel, invocation.ArgumentList.Arguments[1].Expression, "System.Globalization.NumberStyles")
            || !IsInvariantCulture(semanticModel, invocation.ArgumentList.Arguments[2].Expression))
        {
            return new RoslynCapabilityViolation(
                "LOOM.EXPR.SECURITY.INVARIANT_PARSE_REQUIRED",
                "Numeric parsing requires explicit NumberStyles and CultureInfo.InvariantCulture.",
                "Use the four-argument TryParse overload with an approved NumberStyles value and invariant culture.");
        }

        return null;
    }

    private static RoslynCapabilityViolation? ValidateBoundedCollection(SemanticModel semanticModel, InvocationExpressionSyntax invocation, IMethodSymbol method)
    {
        if (!IsBoundedCollectionSource(semanticModel, invocation))
        {
            return new RoslynCapabilityViolation(
                "LOOM.EXPR.RESOURCE.BOUNDED_COLLECTION_REQUIRED",
                "Collection predicates are allowed only on a collection materialized from a bounded context.Get<T> value.",
                "Read a bounded primitive or string collection from context.Get<T> before calling Any, All, Contains, Count, or SequenceEqual.");
        }

        if (method.Name is nameof(Enumerable.Any) or nameof(Enumerable.All)
            && method.Parameters.Length == 2
            && invocation.ArgumentList.Arguments.Count > 0
            && invocation.ArgumentList.Arguments[^1].Expression is not LambdaExpressionSyntax)
        {
            return new RoslynCapabilityViolation(
                "LOOM.EXPR.SECURITY.COLLECTION_PREDICATE_REQUIRED",
                "Any and All predicate overloads require a direct lambda expression.",
                "Use a simple lambda containing only catalog-approved scalar operations.");
        }

        return null;
    }

    private static bool IsAuthorizedCollectionLambda(SemanticModel semanticModel, AnonymousFunctionExpressionSyntax lambda)
    {
        var invocation = lambda.Ancestors().OfType<InvocationExpressionSyntax>().FirstOrDefault();
        if (invocation is null)
        {
            return false;
        }

        var method = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
        return method is not null
            && method.Name is nameof(Enumerable.Any) or nameof(Enumerable.All)
            && FindCapability(method, RoslynCapabilitySurface.Expression)?.Id == RoslynCapabilityCatalog.ExpressionCollections
            && IsBoundedCollectionSource(semanticModel, invocation);
    }

    private static bool IsBoundedCollectionSource(SemanticModel semanticModel, InvocationExpressionSyntax invocation)
    {
        ExpressionSyntax? source = null;
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            source = memberAccess.Expression;
        }
        else if (invocation.ArgumentList.Arguments.Count > 0)
        {
            source = invocation.ArgumentList.Arguments[0].Expression;
        }

        if (source is not InvocationExpressionSyntax getInvocation
            || semanticModel.GetSymbolInfo(getInvocation).Symbol is not IMethodSymbol getMethod
            || !string.Equals(getMethod.Name, "Get", StringComparison.Ordinal)
            || FindCapability(getMethod, RoslynCapabilitySurface.Expression)?.Id != RoslynCapabilityCatalog.ExpressionContextGet)
        {
            return false;
        }

        var type = semanticModel.GetTypeInfo(getInvocation).Type;
        if (!IsSupportedCollectionType(type))
        {
            return false;
        }

        return type is not null && IsBoundedRuntimeCollectionType(type);
    }

    private static bool IsSupportedCollectionType(ITypeSymbol? type)
    {
        if (type is IArrayTypeSymbol arrayType)
        {
            return IsPrimitiveOrString(arrayType.ElementType);
        }

        if (type is not INamedTypeSymbol namedType || namedType.TypeArguments.Length != 1)
        {
            return false;
        }

        var definition = namedType.ConstructedFrom.ToDisplayString();
        return definition is "System.Collections.Generic.IReadOnlyList<T>"
                or "System.Collections.Generic.IReadOnlyCollection<T>"
                or "System.Collections.Generic.IEnumerable<T>"
            && IsPrimitiveOrString(namedType.TypeArguments[0]);
    }

    private static bool IsBoundedRuntimeCollectionType(ITypeSymbol type)
    {
        return type is IArrayTypeSymbol
            || type is INamedTypeSymbol namedType && namedType.ConstructedFrom.ToDisplayString() is
                "System.Collections.Generic.IReadOnlyList<T>"
                or "System.Collections.Generic.IReadOnlyCollection<T>"
                or "System.Collections.Generic.IEnumerable<T>";
    }

    private static bool IsAllowedExpressionType(ITypeSymbol type)
    {
        if (type.SpecialType == SpecialType.System_Object)
        {
            return true;
        }

        if (IsPrimitiveOrString(type))
        {
            return true;
        }

        if (type is IArrayTypeSymbol arrayType)
        {
            return IsPrimitiveOrString(arrayType.ElementType);
        }

        if (type is INamedTypeSymbol { TypeKind: TypeKind.Struct } valueType
            && valueType.ToDisplayString() is "System.Guid" or "System.DateTime" or "System.DateTimeOffset" or "System.TimeSpan")
        {
            return true;
        }

        return IsSupportedCollectionType(type);
    }

    private static bool IsPrimitiveOrString(ITypeSymbol type)
    {
        return type.SpecialType is SpecialType.System_Boolean
            or SpecialType.System_Byte
            or SpecialType.System_SByte
            or SpecialType.System_Int16
            or SpecialType.System_UInt16
            or SpecialType.System_Int32
            or SpecialType.System_UInt32
            or SpecialType.System_Int64
            or SpecialType.System_UInt64
            or SpecialType.System_Single
            or SpecialType.System_Double
            or SpecialType.System_Decimal
            or SpecialType.System_String;
    }
    private static bool IsBuiltInScriptSymbol(ISymbol symbol)
    {
        return symbol is IArrayTypeSymbol arrayType && IsPrimitiveOrString(arrayType.ElementType)
            || symbol is ITypeSymbol { SpecialType: not SpecialType.None };
    }


    private static bool IsCompileTimeString(SemanticModel semanticModel, ExpressionSyntax expression)
        => semanticModel.GetConstantValue(expression) is { HasValue: true, Value: string };

    private static bool IsInvariantCulture(SemanticModel semanticModel, ExpressionSyntax expression)
    {
        var symbol = semanticModel.GetSymbolInfo(expression).Symbol;
        return symbol is IPropertySymbol property
            && string.Equals(property.GetDocumentationCommentId(), "P:System.Globalization.CultureInfo.InvariantCulture", StringComparison.Ordinal);
    }

    private static bool IsAllowedEnumConstant(SemanticModel semanticModel, ExpressionSyntax expression, string enumTypeName)
    {
        var type = semanticModel.GetTypeInfo(expression).Type;
        var constant = semanticModel.GetConstantValue(expression);
        if (type?.ToDisplayString() != enumTypeName || !constant.HasValue)
        {
            return false;
        }

        var value = Convert.ToInt32(constant.Value, CultureInfo.InvariantCulture);
        return enumTypeName switch
        {
            "System.Globalization.NumberStyles" => value == (int)NumberStyles.None
                || value == (int)NumberStyles.Integer
                || value == (int)NumberStyles.Number
                || value == (int)NumberStyles.Float
                || value == (int)NumberStyles.HexNumber
                || value == (int)NumberStyles.Any,
            "System.Globalization.DateTimeStyles" => value == (int)DateTimeStyles.None
                || value == (int)DateTimeStyles.AdjustToUniversal
                || value == (int)DateTimeStyles.AssumeUniversal
                || value == (int)DateTimeStyles.AllowWhiteSpaces
                || value == (int)DateTimeStyles.NoCurrentDateDefault
                || value == (int)DateTimeStyles.RoundtripKind,
            "System.Globalization.TimeSpanStyles" => value == (int)TimeSpanStyles.None
                || value == (int)TimeSpanStyles.AssumeNegative,
            _ => false,
        };
    }

    private static bool TryGetFiniteDouble(object? value, out double result)
    {
        try
        {
            result = Convert.ToDouble(value, CultureInfo.InvariantCulture);
            return double.IsFinite(result);
        }
        catch (Exception) when (value is not null)
        {
            result = 0;
            return false;
        }
    }
    private static RoslynCapabilityDescriptor? FindCapability(ISymbol symbol, RoslynCapabilitySurface surface)
    {
        var matches = RoslynCapabilityCatalog.FindMatches(symbol, surface).ToArray();
        var exact = matches.FirstOrDefault(static descriptor => descriptor.SymbolKind == RoslynCapabilitySymbolKind.ExactSymbol);
        if (exact is not null)
        {
            return exact;
        }

        if (symbol is IMethodSymbol { MethodKind: MethodKind.Constructor } constructor)
        {
            return RoslynCapabilityCatalog.FindMatches(constructor.ContainingType, surface)
                .FirstOrDefault(static descriptor => descriptor.SymbolKind == RoslynCapabilitySymbolKind.ExactSymbol);
        }

        return symbol is IMethodSymbol { ReducedFrom: { } reducedFrom }
            ? RoslynCapabilityCatalog.FindMatches(reducedFrom, surface).FirstOrDefault()
            : matches.FirstOrDefault();
    }


    private static RoslynCapabilityViolation MissingDeclaration(RoslynCapabilityDescriptor descriptor)
    {
        return new RoslynCapabilityViolation(
            "LOOM.EXPR.CONTRACT.CAPABILITY_NOT_DECLARED",
            $"The expression uses capability '{descriptor.Id}', but it was not declared in requiredExpressionCapabilities.",
            $"Add '{descriptor.Id}' to requiredExpressionCapabilities.",
            descriptor.Id);
    }

    private static RoslynCapabilityViolation UnapprovedExpressionSymbol(ISymbol symbol)
    {
        if (symbol is IMethodSymbol method
            && method.ContainingType.ToDisplayString() == "System.Text.RegularExpressions.Regex"
            && method.Name is nameof(Regex.IsMatch)
                or nameof(Regex.Match)
                or nameof(Regex.Matches)
                or nameof(Regex.Count)
                or nameof(Regex.Replace)
                or nameof(Regex.Split))
        {
            return new RoslynCapabilityViolation(
                "LOOM.EXPR.SECURITY.REGEX_TIMEOUT_REQUIRED",
                "Regex matching requires the overload that includes RegexOptions and TimeSpan.",
                "Use the static Regex overload with options and an explicit finite timeout.");
        }
        return new RoslynCapabilityViolation(
            "LOOM.EXPR.SECURITY.UNAPPROVED_API",
            $"The API symbol '{symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}' is not approved for workflow expressions.",
            "Use baseline context access or declare and use an exact capability from the current expression catalog.");
    }

    private static bool IsForbiddenScriptSymbol(ISymbol symbol)
    {
        if (symbol is IMethodSymbol method && ForbiddenScriptMemberNames.Contains(method.Name))
        {
            return true;
        }

        if (symbol is IPropertySymbol property && ForbiddenScriptMemberNames.Contains(property.Name))
        {
            return true;
        }

        var containingType = GetContainingType(symbol);
        if (containingType is null)
        {
            return false;
        }

        var namespaceName = containingType.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        return IsForbiddenScriptType(containingType);
    }

    private static bool IsForbiddenScriptType(INamedTypeSymbol containingType)
    {
        var namespaceName = containingType.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        return ForbiddenScriptNamespaces.Any(forbidden => namespaceName.Equals(forbidden, StringComparison.Ordinal)
                || namespaceName.StartsWith(forbidden + ".", StringComparison.Ordinal))
            || ForbiddenScriptTypeNames.Contains(containingType.Name);
    }

    private static ISymbol? GetSymbol(SemanticModel semanticModel, SyntaxNode node)
    {
        return node switch
        {
            InvocationExpressionSyntax invocation => semanticModel.GetSymbolInfo(invocation).Symbol,
            ObjectCreationExpressionSyntax creation => semanticModel.GetSymbolInfo(creation).Symbol,
            MemberAccessExpressionSyntax memberAccess => semanticModel.GetSymbolInfo(memberAccess).Symbol,
            ElementAccessExpressionSyntax elementAccess => semanticModel.GetSymbolInfo(elementAccess).Symbol,
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
