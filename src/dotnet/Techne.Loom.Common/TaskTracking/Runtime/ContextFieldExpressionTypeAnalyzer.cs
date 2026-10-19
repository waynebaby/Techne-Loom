using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Techne.Loom.Common.TaskTracking.Runtime;

internal sealed record ContextFieldTypeViolation(
    string Code,
    string Message,
    string SuggestedFix,
    TextSpan SourceSpan);

internal sealed record ContextFieldExpressionAccess(string Path, bool IsCatalogDeclared);

internal sealed record ContextFieldExpressionAnalysis(
    IReadOnlyList<ContextFieldExpressionAccess> Accesses,
    ContextFieldTypeViolation? Violation);

internal static class ContextFieldExpressionTypeAnalyzer
{
    private const string ExpressionContextType = "Techne.Loom.Common.TaskTracking.Runtime.ExpressionRuntimeContext";

    public static ContextFieldExpressionAnalysis Analyze(
        SemanticModel semanticModel,
        ExpressionSyntax expression,
        ContextFieldCatalog catalog)
    {
        var accesses = new List<ContextFieldExpressionAccess>();
        foreach (var invocation in expression.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>())
        {
            if (semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method
                || !string.Equals(method.ContainingType.ToDisplayString(), ExpressionContextType, StringComparison.Ordinal)
                || method.Name is not ("Get" or "Has")
                || invocation.ArgumentList.Arguments.Count != 1)
            {
                continue;
            }

            var catalogDeclared = TryResolvePath(semanticModel, invocation.ArgumentList.Arguments[0].Expression, catalog, out var field, out var path);
            accesses.Add(new ContextFieldExpressionAccess(path, catalogDeclared));
            if (!catalogDeclared)
            {
                return new ContextFieldExpressionAnalysis(
                    accesses,
                    new ContextFieldTypeViolation(
                        "LOOM.EXPR.CONTEXT.FIELD_UNDECLARED",
                        $"Context path '{path}' is not declared by the owning context_fields catalog or a typed dynamic container.",
                        "Declare this context path in the owning contract, or use a path beneath an object field with a declared valueType.",
                        invocation.Span));
            }

            if (ExceedsRuntimeContextPathDepth(path))
            {
                return new ContextFieldExpressionAnalysis(
                    accesses,
                    new ContextFieldTypeViolation(
                        "LOOM.EXPR.RESOURCE.CONTEXT_DEPTH",
                        $"Context path '{path}' exceeds the {ExpressionRuntimeContext.MaxContextPathSegments}-segment expression limit.",
                        $"Use a context path with no more than {ExpressionRuntimeContext.MaxContextPathSegments} segments.",
                        invocation.Span));
            }

            if (method.Name == "Has")
            {
                continue;
            }

            if (method.TypeArguments.Length != 1 || !MatchesConsumerType(method.TypeArguments[0], field))
            {
                var actualType = method.TypeArguments.Length == 1
                    ? method.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
                    : "unknown";
                return new ContextFieldExpressionAnalysis(
                    accesses,
                    new ContextFieldTypeViolation(
                        "LOOM.EXPR.CONTEXT.FIELD_TYPE_MISMATCH",
                        $"Context path '{path}' is declared as JSON '{field.Type}', but this expression reads it as '{actualType}'.",
                        $"Use context.Get<{CanonicalConsumerType(field)}>(\"{path}\") or correct the owning contract if its declared type is wrong.",
                        invocation.Span));
            }
        }

        foreach (var elementAccess in expression.DescendantNodesAndSelf().OfType<ElementAccessExpressionSyntax>())
        {
            if (semanticModel.GetSymbolInfo(elementAccess).Symbol is not IPropertySymbol property
                || !property.IsIndexer
                || !string.Equals(property.ContainingType.ToDisplayString(), ExpressionContextType, StringComparison.Ordinal)
                || elementAccess.ArgumentList.Arguments.Count != 1)
            {
                continue;
            }

            var catalogDeclared = TryResolvePath(semanticModel, elementAccess.ArgumentList.Arguments[0].Expression, catalog, out var field, out var path);
            accesses.Add(new ContextFieldExpressionAccess(path, catalogDeclared));
            if (!catalogDeclared)
            {
                return new ContextFieldExpressionAnalysis(
                    accesses,
                    new ContextFieldTypeViolation(
                        "LOOM.EXPR.CONTEXT.FIELD_UNDECLARED",
                        $"Context path '{path}' is not declared by the owning context_fields catalog.",
                        "Declare this path in the owning contract and read it with context.Get<T>(\"path\") so its consumer type is explicit.",
                        elementAccess.Span));
            }

            if (ExceedsRuntimeContextPathDepth(path))
            {
                return new ContextFieldExpressionAnalysis(
                    accesses,
                    new ContextFieldTypeViolation(
                        "LOOM.EXPR.RESOURCE.CONTEXT_DEPTH",
                        $"Context path '{path}' exceeds the {ExpressionRuntimeContext.MaxContextPathSegments}-segment expression limit.",
                        $"Use a context path with no more than {ExpressionRuntimeContext.MaxContextPathSegments} segments.",
                        elementAccess.Span));
            }

            if (!string.Equals(field.Type, "object", StringComparison.Ordinal) || field.ValueType is not null)
            {
                return new ContextFieldExpressionAnalysis(
                    accesses,
                    new ContextFieldTypeViolation(
                        "LOOM.EXPR.CONTEXT.INDEXER_TYPE_UNDECLARED",
                        $"The context indexer returns object for typed field '{path}' ({field.Type}) and cannot prove the consumer type.",
                        $"Use context.Get<{CanonicalConsumerType(field)}>(\"{path}\") so the expression type matches the owning catalog.",
                        elementAccess.Span));
            }
        }

        return new ContextFieldExpressionAnalysis(accesses, null);
    }

    private static bool ExceedsRuntimeContextPathDepth(string path)
    {
        var segmentCount = path.Split('.', StringSplitOptions.RemoveEmptyEntries).Length;
        if (path.EndsWith(".", StringComparison.Ordinal))
        {
            segmentCount++;
        }

        return segmentCount > ExpressionRuntimeContext.MaxContextPathSegments;
    }

    private static bool TryResolvePath(
        SemanticModel semanticModel,
        ExpressionSyntax pathExpression,
        ContextFieldCatalog catalog,
        out ContextFieldDefinition field,
        out string path)
    {
        if (semanticModel.GetConstantValue(pathExpression) is { HasValue: true, Value: string constantPath })
        {
            path = constantPath;
            if (catalog.TryGetField(path, out field!))
            {
                return true;
            }

            var separator = path.LastIndexOf(".", StringComparison.Ordinal);
            while (separator > 0)
            {
                var parentPath = path[..separator];
                if (catalog.TryGetField(parentPath, out var parent)
                    && parent.Type == "object"
                    && parent.ValueType is not null
                    && !path[(separator + 1)..].Contains('.', StringComparison.Ordinal))
                {
                    field = parent with
                    {
                        Path = path,
                        Type = parent.ValueType,
                        Nullable = false,
                        ItemType = null,
                        ValueType = null,
                        AllowedValues = [],
                        SuggestedValues = [],
                    };
                    return true;
                }

                separator = path.LastIndexOf('.', separator - 1);
            }

            field = null!;
            return false;
        }

        if (TryGetDynamicPrefix(semanticModel, pathExpression, out var prefix))
        {
            path = prefix;
            var parentPath = prefix.EndsWith(".", StringComparison.Ordinal)
                ? prefix[..^1]
                : prefix;
            if (prefix.EndsWith(".", StringComparison.Ordinal)
                && catalog.TryGetField(parentPath, out var parent)
                && parent.Type == "object"
                && parent.ValueType is not null)
            {
                field = parent with
                {
                    Path = prefix,
                    Type = parent.ValueType,
                    Nullable = false,
                    ItemType = null,
                    ValueType = null,
                    AllowedValues = [],
                    SuggestedValues = [],
                };
                return true;
            }

            field = null!;
            return false;
        }

        path = pathExpression.ToString();
        field = null!;
        return false;
    }

    private static bool TryGetDynamicPrefix(SemanticModel semanticModel, ExpressionSyntax expression, out string prefix)
    {
        if (expression is BinaryExpressionSyntax binary
            && binary.IsKind(SyntaxKind.AddExpression)
            && semanticModel.GetConstantValue(binary.Left) is { HasValue: true, Value: string leftPrefix }
            && !string.IsNullOrEmpty(leftPrefix))
        {
            prefix = leftPrefix;
            return true;
        }

        if (expression is InterpolatedStringExpressionSyntax interpolated)
        {
            var interpolations = interpolated.Contents.OfType<InterpolationSyntax>().ToArray();
            var firstInterpolation = interpolations.FirstOrDefault();
            if (interpolations.Length == 1
                && firstInterpolation is not null
                && firstInterpolation.AlignmentClause is null
                && firstInterpolation.FormatClause is null
                && interpolated.Contents.LastOrDefault() == firstInterpolation)
            {
                var text = interpolated.Contents
                    .OfType<InterpolatedStringTextSyntax>()
                    .TakeWhile(item => item.SpanStart < firstInterpolation.SpanStart)
                    .Select(static item => item.TextToken.ValueText);
                prefix = string.Concat(text);
                return !string.IsNullOrEmpty(prefix);
            }
        }

        prefix = string.Empty;
        return false;
    }

    private static bool MatchesConsumerType(ITypeSymbol consumerType, ContextFieldDefinition field)
    {
        var effectiveType = consumerType;
        var nullableValueType = false;
        if (consumerType is INamedTypeSymbol nullableType
            && nullableType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            effectiveType = nullableType.TypeArguments[0];
            nullableValueType = true;
        }

        var consumerIsNullable = effectiveType.IsValueType
            ? nullableValueType
            : consumerType.NullableAnnotation == NullableAnnotation.Annotated;
        if (field.Nullable != consumerIsNullable)
        {
            return false;
        }

        return field.Type switch
        {
            "string" => effectiveType.SpecialType == SpecialType.System_String,
            "boolean" => effectiveType.SpecialType == SpecialType.System_Boolean,
            "integer" => effectiveType.SpecialType == SpecialType.System_Int64,
            "number" => effectiveType.SpecialType == SpecialType.System_Decimal,
            "object" => effectiveType.SpecialType == SpecialType.System_Object,
            "array" => MatchesArrayType(effectiveType, field.ItemType),
            _ => false,
        };
    }

    private static bool MatchesArrayType(ITypeSymbol consumerType, string? itemType)
    {
        if (itemType is not ("string" or "boolean" or "integer" or "number"))
        {
            return false;
        }

        var elementType = consumerType switch
        {
            IArrayTypeSymbol arrayType => arrayType.ElementType,
            INamedTypeSymbol namedType when namedType.IsGenericType
                && namedType.ContainingNamespace.ToDisplayString() == "System.Collections.Generic"
                && namedType.Name is "IReadOnlyList" or "IReadOnlyCollection" or "IEnumerable"
                => namedType.TypeArguments[0],
            _ => null,
        };
        if (elementType is null
            || elementType is INamedTypeSymbol nullableElement
                && nullableElement.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
            || elementType.IsReferenceType && elementType.NullableAnnotation == NullableAnnotation.Annotated)
        {
            return false;
        }

        return MatchesScalarType(elementType, itemType);
    }

    private static bool MatchesScalarType(ITypeSymbol symbol, string type)
    {
        return type switch
        {
            "string" => symbol.SpecialType == SpecialType.System_String,
            "boolean" => symbol.SpecialType == SpecialType.System_Boolean,
            "integer" => symbol.SpecialType == SpecialType.System_Int64,
            "number" => symbol.SpecialType == SpecialType.System_Decimal,
            _ => false,
        };
    }
    private static string CanonicalConsumerType(ContextFieldDefinition field)
    {
        var type = field.Type switch
        {
            "string" => "string",
            "boolean" => "bool",
            "integer" => "long",
            "number" => "decimal",
            "object" => "object",
            "array" => $"IReadOnlyList<{CanonicalScalarType(field.ItemType)}>",
            _ => "object",
        };
        return field.Nullable ? type + "?" : type;
    }

    private static string CanonicalScalarType(string? type)
        => type switch
        {
            "string" => "string",
            "boolean" => "bool",
            "integer" => "long",
            "number" => "decimal",
            "object" => "object",
            "array" => "object",
            _ => "object",
        };
}
