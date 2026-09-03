using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public enum RoslynCapabilitySurface
{
    Expression,
    Script,
}

public enum RoslynCapabilitySymbolKind
{
    ExactSymbol,
    AssemblyFamily,
}

public enum RoslynCapabilityConstraint
{
    None,
    ConstantTimeSpan,
    OrdinalComparison,
    RegexMatchTimeout,
    RegexOptions,
    BoundedCollection,
    InvariantParsing,
    ReadOnlyJson,
    InMemoryHashing,
}

public sealed class RoslynCapabilityDescriptor
{
    public RoslynCapabilityDescriptor(
        string id,
        RoslynCapabilitySurface surface,
        RoslynCapabilitySymbolKind symbolKind,
        string symbolId,
        string requiredAssembly,
        RoslynCapabilityConstraint constraint,
        string diagnosticGuidance,
        string documentationId,
        string? namespacePrefix = null,
        IEnumerable<string>? additionalSymbolIds = null,
        IEnumerable<string>? additionalAssemblies = null)
    {
        Id = id;
        Surface = surface;
        SymbolKind = symbolKind;
        RequiredAssembly = requiredAssembly;
        Constraint = constraint;
        DiagnosticGuidance = diagnosticGuidance;
        DocumentationId = documentationId;
        NamespacePrefix = namespacePrefix;
        var allSymbolIds = new List<string> { symbolId };
        if (additionalSymbolIds is not null)
        {
            allSymbolIds.AddRange(additionalSymbolIds);
        }

        SymbolIds = allSymbolIds.ToImmutableArray();
        RequiredAssemblies = new[] { requiredAssembly }
            .Concat(additionalAssemblies ?? [])
            .ToImmutableHashSet(StringComparer.Ordinal);
    }

    public string Id { get; }

    public RoslynCapabilitySurface Surface { get; }

    public RoslynCapabilitySymbolKind SymbolKind { get; }

    public ImmutableArray<string> SymbolIds { get; }

    public string SymbolId => SymbolIds[0];

    public string RequiredAssembly { get; }

    public RoslynCapabilityConstraint Constraint { get; }

    public string DiagnosticGuidance { get; }

    public string DocumentationId { get; }

    public string? NamespacePrefix { get; }
    public ImmutableHashSet<string> RequiredAssemblies { get; }


    public bool Matches(ISymbol symbol)
    {
        if (!RequiredAssemblies.Contains(symbol.ContainingAssembly?.Name ?? string.Empty))
        {
            return false;
        }

        if (SymbolKind == RoslynCapabilitySymbolKind.AssemblyFamily)
        {
            if (NamespacePrefix is null)
            {
                return true;
            }

            var namespaceName = GetContainingNamespace(symbol);
            return namespaceName.Equals(NamespacePrefix, StringComparison.Ordinal)
                || namespaceName.StartsWith(NamespacePrefix + ".", StringComparison.Ordinal);
        }

        var candidate = symbol.OriginalDefinition;
        var documentationCommentId = candidate.GetDocumentationCommentId();
        return documentationCommentId is not null
            && SymbolIds.Any(symbolId => string.Equals(symbolId, documentationCommentId, StringComparison.Ordinal));
    }

    private static string GetContainingNamespace(ISymbol symbol)
    {
        return symbol switch
        {
            INamespaceSymbol namespaceSymbol => namespaceSymbol.ToDisplayString(),
            _ => symbol.ContainingNamespace?.ToDisplayString() ?? string.Empty,
        };
    }
}

public static class RoslynCapabilityCatalog
{
    public const string ExpressionContextGet = ExpressionCapabilityIds.ContextGet;
    public const string ExpressionContextHas = ExpressionCapabilityIds.ContextHas;
    public const string ExpressionContextIndexer = ExpressionCapabilityIds.ContextIndexer;
    public const string ExpressionString = ExpressionCapabilityIds.StringOrdinal;
    public const string ExpressionMath = ExpressionCapabilityIds.Math;
    public const string ExpressionTimeSpan = ExpressionCapabilityIds.TimeSpan;
    public const string ExpressionRegex = ExpressionCapabilityIds.Regex;
    public const string ExpressionParsing = ExpressionCapabilityIds.InvariantParsing;
    public const string ExpressionCollections = ExpressionCapabilityIds.BoundedCollections;
    public const string ScriptSystemCore = "loom.script.system.core";
    public const string ScriptSystemRuntime = "loom.script.system.runtime";
    public const string ScriptSystemCollections = "loom.script.system.collections";
    public const string ScriptSystemLinq = "loom.script.system.linq";
    public const string ScriptSystemObjectModel = "loom.script.system.object-model";
    public const string ScriptSystemRuntimeExtensions = "loom.script.system.runtime-extensions";
    public const string ScriptLoomModel = "loom.script.loom-model";
    public const string ScriptRegex = "loom.script.regex";
    public const string ScriptJson = "loom.script.json.read-only";
    public const string ScriptParsing = "loom.script.parsing.invariant";
    public const string ScriptHashing = "loom.script.hashing.sha256";
    public const string ScriptEncoding = "loom.script.encoding.utf8";
    public const string ScriptConversion = "loom.script.encoding.hex-base64";

    private static readonly ImmutableArray<RoslynCapabilityDescriptor> entries =
    [
        Exact(
            ExpressionContextGet,
            RoslynCapabilitySurface.Expression,
            "Techne.Loom.Common",
            RoslynCapabilityConstraint.None,
            "Read a typed value from the bounded workflow context.",
            "tools.expression.context.get",
            "M:Techne.Loom.Common.TaskTracking.Runtime.ExpressionRuntimeContext.Get``1(System.String)"),
        Exact(
            ExpressionContextHas,
            RoslynCapabilitySurface.Expression,
            "Techne.Loom.Common",
            RoslynCapabilityConstraint.None,
            "Check whether a context path exists without mutating the workflow.",
            "tools.expression.context.has",
            "M:Techne.Loom.Common.TaskTracking.Runtime.ExpressionRuntimeContext.Has(System.String)"),
        Exact(
            ExpressionContextIndexer,
            RoslynCapabilitySurface.Expression,
            "Techne.Loom.Common",
            RoslynCapabilityConstraint.None,
            "Read one value from the bounded workflow context by path.",
            "tools.expression.context.indexer",
            "P:Techne.Loom.Common.TaskTracking.Runtime.ExpressionRuntimeContext.Item(System.String)"),
        Exact(
            ExpressionString,
            RoslynCapabilitySurface.Expression,
            "System.Private.CoreLib",
            RoslynCapabilityConstraint.OrdinalComparison,
            "Use deterministic ordinal or ordinal-ignore-case string predicates.",
            "tools.expression.string.ordinal",
            "M:System.String.IsNullOrEmpty(System.String)",
            "M:System.String.IsNullOrWhiteSpace(System.String)",
            "M:System.String.Equals(System.String,System.StringComparison)",
            "M:System.String.Equals(System.String,System.String,System.StringComparison)",
            "M:System.String.Contains(System.String,System.StringComparison)",
            "M:System.String.StartsWith(System.String,System.StringComparison)",
            "M:System.String.EndsWith(System.String,System.StringComparison)",
            "M:System.String.IndexOf(System.String,System.StringComparison)",
            "M:System.String.IndexOf(System.String,System.Int32,System.StringComparison)",
            "M:System.String.LastIndexOf(System.String,System.StringComparison)",
            "M:System.String.LastIndexOf(System.String,System.Int32,System.StringComparison)",
            "F:System.StringComparison.Ordinal",
            "F:System.StringComparison.OrdinalIgnoreCase"),
        Exact(
            ExpressionMath,
            RoslynCapabilitySurface.Expression,
            "System.Private.CoreLib",
            RoslynCapabilityConstraint.None,
            "Use the approved constant-time Math overloads for primitive numeric values.",
            "tools.expression.math",
            "M:System.Math.Abs(System.Decimal)",
            "M:System.Math.Abs(System.Double)",
            "M:System.Math.Abs(System.Int16)",
            "M:System.Math.Abs(System.Int32)",
            "M:System.Math.Abs(System.Int64)",
            "M:System.Math.Abs(System.SByte)",
            "M:System.Math.Abs(System.Single)",
            "M:System.Math.Ceiling(System.Decimal)",
            "M:System.Math.Ceiling(System.Double)",
            "M:System.Math.Clamp(System.Byte,System.Byte,System.Byte)",
            "M:System.Math.Clamp(System.Decimal,System.Decimal,System.Decimal)",
            "M:System.Math.Clamp(System.Double,System.Double,System.Double)",
            "M:System.Math.Clamp(System.Int16,System.Int16,System.Int16)",
            "M:System.Math.Clamp(System.Int32,System.Int32,System.Int32)",
            "M:System.Math.Clamp(System.Int64,System.Int64,System.Int64)",
            "M:System.Math.Clamp(System.SByte,System.SByte,System.SByte)",
            "M:System.Math.Clamp(System.Single,System.Single,System.Single)",
            "M:System.Math.Clamp(System.UInt16,System.UInt16,System.UInt16)",
            "M:System.Math.Clamp(System.UInt32,System.UInt32,System.UInt32)",
            "M:System.Math.Clamp(System.UInt64,System.UInt64,System.UInt64)",
            "M:System.Math.Floor(System.Decimal)",
            "M:System.Math.Floor(System.Double)",
            "M:System.Math.Max(System.Byte,System.Byte)",
            "M:System.Math.Max(System.Decimal,System.Decimal)",
            "M:System.Math.Max(System.Double,System.Double)",
            "M:System.Math.Max(System.Int16,System.Int16)",
            "M:System.Math.Max(System.Int32,System.Int32)",
            "M:System.Math.Max(System.Int64,System.Int64)",
            "M:System.Math.Max(System.SByte,System.SByte)",
            "M:System.Math.Max(System.Single,System.Single)",
            "M:System.Math.Max(System.UInt16,System.UInt16)",
            "M:System.Math.Max(System.UInt32,System.UInt32)",
            "M:System.Math.Max(System.UInt64,System.UInt64)",
            "M:System.Math.Min(System.Byte,System.Byte)",
            "M:System.Math.Min(System.Decimal,System.Decimal)",
            "M:System.Math.Min(System.Double,System.Double)",
            "M:System.Math.Min(System.Int16,System.Int16)",
            "M:System.Math.Min(System.Int32,System.Int32)",
            "M:System.Math.Min(System.Int64,System.Int64)",
            "M:System.Math.Min(System.SByte,System.SByte)",
            "M:System.Math.Min(System.Single,System.Single)",
            "M:System.Math.Min(System.UInt16,System.UInt16)",
            "M:System.Math.Min(System.UInt32,System.UInt32)",
            "M:System.Math.Min(System.UInt64,System.UInt64)",
            "M:System.Math.Round(System.Decimal)",
            "M:System.Math.Round(System.Decimal,System.Int32)",
            "M:System.Math.Round(System.Decimal,System.Int32,System.MidpointRounding)",
            "M:System.Math.Round(System.Decimal,System.MidpointRounding)",
            "M:System.Math.Round(System.Double)",
            "M:System.Math.Round(System.Double,System.Int32)",
            "M:System.Math.Round(System.Double,System.Int32,System.MidpointRounding)",
            "M:System.Math.Round(System.Double,System.MidpointRounding)",
            "M:System.Math.Sign(System.Decimal)",
            "M:System.Math.Sign(System.Double)",
            "M:System.Math.Sign(System.Int16)",
            "M:System.Math.Sign(System.Int32)",
            "M:System.Math.Sign(System.Int64)",
            "M:System.Math.Sign(System.SByte)",
            "M:System.Math.Sign(System.Single)",
            "M:System.Math.Truncate(System.Decimal)",
            "M:System.Math.Truncate(System.Double)",
            "F:System.MidpointRounding.ToEven",
            "F:System.MidpointRounding.AwayFromZero",
            "F:System.MidpointRounding.ToZero",
            "F:System.MidpointRounding.ToNegativeInfinity",
            "F:System.MidpointRounding.ToPositiveInfinity"),
        Exact(
            ExpressionTimeSpan,
            RoslynCapabilitySurface.Expression,
            "System.Private.CoreLib",
            RoslynCapabilityConstraint.ConstantTimeSpan,
            "Construct a finite timeout only from a compile-time numeric constant.",
            "tools.expression.timespan",
            "M:System.TimeSpan.FromMilliseconds(System.Double)",
            "M:System.TimeSpan.FromSeconds(System.Double)",
            "M:System.TimeSpan.FromSeconds(System.Int64)"),
        Exact(
            ExpressionRegex,
            RoslynCapabilitySurface.Expression,
            "System.Text.RegularExpressions",
            RoslynCapabilityConstraint.RegexMatchTimeout,
            "Use native static Regex matching with an explicit finite timeout of at most five seconds.",
            "tools.expression.regex",
            "T:System.Text.RegularExpressions.Regex",
            "T:System.Text.RegularExpressions.RegexOptions",
            "M:System.Text.RegularExpressions.Regex.IsMatch(System.String,System.String,System.Text.RegularExpressions.RegexOptions,System.TimeSpan)",
            "M:System.Text.RegularExpressions.Regex.Match(System.String,System.String,System.Text.RegularExpressions.RegexOptions,System.TimeSpan)",
            "M:System.Text.RegularExpressions.Regex.Matches(System.String,System.String,System.Text.RegularExpressions.RegexOptions,System.TimeSpan)",
            "M:System.Text.RegularExpressions.Regex.Count(System.String,System.String,System.Text.RegularExpressions.RegexOptions,System.TimeSpan)",
            "M:System.Text.RegularExpressions.Regex.Replace(System.String,System.String,System.String,System.Text.RegularExpressions.RegexOptions,System.TimeSpan)",
            "M:System.Text.RegularExpressions.Regex.Split(System.String,System.String,System.Text.RegularExpressions.RegexOptions,System.TimeSpan)",
            "M:System.Text.RegularExpressions.Regex.Escape(System.String)",
            "M:System.Text.RegularExpressions.Regex.Unescape(System.String)",
            "T:System.Text.RegularExpressions.Group",
            "T:System.Text.RegularExpressions.Capture",
            "P:System.Text.RegularExpressions.Capture.Value",
            "P:System.Text.RegularExpressions.Group.Success",
            "T:System.Text.RegularExpressions.Match",
            "T:System.Text.RegularExpressions.MatchCollection",
            "P:System.Text.RegularExpressions.MatchCollection.Count",
            "F:System.Text.RegularExpressions.RegexOptions.None",
            "F:System.Text.RegularExpressions.RegexOptions.CultureInvariant",
            "F:System.Text.RegularExpressions.RegexOptions.IgnoreCase",
            "F:System.Text.RegularExpressions.RegexOptions.Multiline",
            "F:System.Text.RegularExpressions.RegexOptions.Singleline",
            "F:System.Text.RegularExpressions.RegexOptions.ExplicitCapture",
            "F:System.Text.RegularExpressions.RegexOptions.IgnorePatternWhitespace",
            "F:System.Text.RegularExpressions.RegexOptions.NonBacktracking"),
        Exact(
            ExpressionParsing,
            RoslynCapabilitySurface.Expression,
            "System.Private.CoreLib",
            RoslynCapabilityConstraint.InvariantParsing,
            "Parse invariant numeric, identifier, date, and duration values with explicit styles and formats.",
            "tools.expression.parsing.invariant",
            "M:System.Byte.TryParse(System.String,System.Globalization.NumberStyles,System.IFormatProvider,System.Byte@)",
            "M:System.SByte.TryParse(System.String,System.Globalization.NumberStyles,System.IFormatProvider,System.SByte@)",
            "M:System.Int16.TryParse(System.String,System.Globalization.NumberStyles,System.IFormatProvider,System.Int16@)",
            "M:System.Int32.TryParse(System.String,System.Globalization.NumberStyles,System.IFormatProvider,System.Int32@)",
            "M:System.Int64.TryParse(System.String,System.Globalization.NumberStyles,System.IFormatProvider,System.Int64@)",
            "M:System.UInt16.TryParse(System.String,System.Globalization.NumberStyles,System.IFormatProvider,System.UInt16@)",
            "M:System.UInt32.TryParse(System.String,System.Globalization.NumberStyles,System.IFormatProvider,System.UInt32@)",
            "M:System.UInt64.TryParse(System.String,System.Globalization.NumberStyles,System.IFormatProvider,System.UInt64@)",
            "M:System.Single.TryParse(System.String,System.Globalization.NumberStyles,System.IFormatProvider,System.Single@)",
            "M:System.Double.TryParse(System.String,System.Globalization.NumberStyles,System.IFormatProvider,System.Double@)",
            "M:System.Decimal.TryParse(System.String,System.Globalization.NumberStyles,System.IFormatProvider,System.Decimal@)",
            "M:System.Guid.TryParse(System.String,System.Guid@)",
            "M:System.Guid.TryParse(System.String,System.IFormatProvider,System.Guid@)",
            "M:System.DateTimeOffset.TryParseExact(System.String,System.String,System.IFormatProvider,System.Globalization.DateTimeStyles,System.DateTimeOffset@)",
            "M:System.TimeSpan.TryParseExact(System.String,System.String,System.IFormatProvider,System.Globalization.TimeSpanStyles,System.TimeSpan@)",
            "P:System.Globalization.CultureInfo.InvariantCulture",
            "F:System.Globalization.NumberStyles.None",
            "F:System.Globalization.NumberStyles.Integer",
            "F:System.Globalization.NumberStyles.Number",
            "F:System.Globalization.NumberStyles.Float",
            "F:System.Globalization.NumberStyles.HexNumber",
            "F:System.Globalization.NumberStyles.Any",
            "F:System.Globalization.DateTimeStyles.None",
            "F:System.Globalization.DateTimeStyles.AdjustToUniversal",
            "F:System.Globalization.DateTimeStyles.AssumeUniversal",
            "F:System.Globalization.DateTimeStyles.AllowWhiteSpaces",
            "F:System.Globalization.DateTimeStyles.NoCurrentDateDefault",
            "F:System.Globalization.DateTimeStyles.RoundtripKind",
            "F:System.Globalization.TimeSpanStyles.None",
            "F:System.Globalization.TimeSpanStyles.AssumeNegative"),
        ExactWithAssemblies(
            ExpressionCollections,
            RoslynCapabilitySurface.Expression,
            "System.Linq",
            "System.Private.CoreLib",
            RoslynCapabilityConstraint.BoundedCollection,
            "Use only bounded context collections with Any, All, Contains, Count, or SequenceEqual.",
            "tools.expression.collections.bounded",
            "M:System.Linq.Enumerable.Any``1(System.Collections.Generic.IEnumerable{``0})",
            "M:System.Linq.Enumerable.Any``1(System.Collections.Generic.IEnumerable{``0},System.Func{``0,System.Boolean})",
            "M:System.Linq.Enumerable.All``1(System.Collections.Generic.IEnumerable{``0},System.Func{``0,System.Boolean})",
            "M:System.Linq.Enumerable.Contains``1(System.Collections.Generic.IEnumerable{``0},``0)",
            "M:System.Linq.Enumerable.Count``1(System.Collections.Generic.IEnumerable{``0})",
            "M:System.Linq.Enumerable.Count``1(System.Collections.Generic.IEnumerable{``0},System.Func{``0,System.Boolean})",
            "M:System.Linq.Enumerable.SequenceEqual``1(System.Collections.Generic.IEnumerable{``0},System.Collections.Generic.IEnumerable{``0})",
            "P:System.Collections.Generic.IReadOnlyCollection`1.Count"),
        Exact(
            ScriptSystemCore,
            RoslynCapabilitySurface.Script,
            "System.Private.CoreLib",
            RoslynCapabilityConstraint.None,
            "Use the explicitly approved core helpers for deterministic workflow scripts.",
            "tools.script.system.core",
            "T:System.StringComparer",
            "P:System.StringComparer.Ordinal",
            "T:System.Convert",
            "M:System.Convert.ToString(System.Object)",
            "F:System.String.Empty",
            "T:System.Guid",
            "M:System.Guid.ToString"),
        Exact(
            ScriptSystemRuntime,
            RoslynCapabilitySurface.Script,
            "System.Private.CoreLib",
            RoslynCapabilityConstraint.None,
            "Use the explicitly approved synchronous runtime value types.",
            "tools.script.system.runtime",
            "T:System.TimeSpan",
            "M:System.TimeSpan.FromSeconds(System.Double)",
            "M:System.TimeSpan.FromMilliseconds(System.Double)",
            "M:System.TimeSpan.FromSeconds(System.Int64)"),
        Exact(
            ScriptSystemCollections,
            RoslynCapabilitySurface.Script,
            "System.Private.CoreLib",
            RoslynCapabilityConstraint.None,
            "Use the explicitly approved generic in-memory collection types and bounded accessors.",
            "tools.script.collections",
            "T:System.Collections.Generic.Dictionary`2",
            "T:System.Collections.Generic.IReadOnlyDictionary`2",
            "T:System.Collections.Generic.IReadOnlyList`1",
            "T:System.Collections.Generic.IReadOnlyCollection`1",
            "T:System.Collections.Generic.IEnumerable`1",
            "T:System.Collections.Generic.List`1",
            "T:System.Collections.Generic.KeyValuePair`2",
            "P:System.Collections.Generic.Dictionary`2.Item(`0)",
            "P:System.Collections.Generic.IReadOnlyDictionary`2.Item(`0)",
            "P:System.Collections.Generic.IReadOnlyDictionary`2.Values",
            "P:System.Collections.Generic.IReadOnlyList`1.Item(System.Int32)",
            "P:System.Collections.Generic.IReadOnlyCollection`1.Count",
            "P:System.Collections.Generic.List`1.Item(System.Int32)"),
        Exact(
            ScriptSystemLinq,
            RoslynCapabilitySurface.Script,
            "System.Linq",
            RoslynCapabilityConstraint.None,
            "Use only explicitly approved synchronous Enumerable methods in scripts.",
            "tools.script.enumerable",
            "T:System.Linq.Enumerable"),
        Exact(
            "loom.script.enumerable.members",
            RoslynCapabilitySurface.Script,
            "System.Linq",
            RoslynCapabilityConstraint.None,
            "Use the explicitly approved synchronous Enumerable member operations in deterministic workflow scripts.",
            "tools.script.enumerable.members",
            "M:System.Linq.Enumerable.Any``1(System.Collections.Generic.IEnumerable{``0},System.Func{``0,System.Boolean})",
            "M:System.Linq.Enumerable.All``1(System.Collections.Generic.IEnumerable{``0},System.Func{``0,System.Boolean})",
            "M:System.Linq.Enumerable.Where``1(System.Collections.Generic.IEnumerable{``0},System.Func{``0,System.Boolean})",
            "M:System.Linq.Enumerable.OfType``1(System.Collections.IEnumerable)",
            "M:System.Linq.Enumerable.ToDictionary``3(System.Collections.Generic.IEnumerable{``0},System.Func{``0,``1},System.Func{``0,``2},System.Collections.Generic.IEqualityComparer{``1})",
            "M:System.Linq.Enumerable.ToList``1(System.Collections.Generic.IEnumerable{``0})"),
        Exact(
            "loom.script.collections.members",
            RoslynCapabilitySurface.Script,
            "System.Private.CoreLib",
            RoslynCapabilityConstraint.None,
            "Use the explicitly approved dictionary and list member operations for deterministic workflow scripts.",
            "tools.script.collections.members",
            "M:System.Collections.Generic.Dictionary`2.#ctor(System.Collections.Generic.IEqualityComparer{`0})",
            "M:System.Collections.Generic.Dictionary`2.ContainsKey(`0)",
            "M:System.Collections.Generic.Dictionary`2.TryGetValue(`0,`1@)",
            "P:System.Collections.Generic.Dictionary`2.Count",
            "M:System.Collections.Generic.List`1.Contains(`0)",
            "P:System.Collections.Generic.KeyValuePair`2.Key",
            "P:System.Collections.Generic.KeyValuePair`2.Value",
            "M:System.Int32.ToString"),
        Exact(
            "loom.script.model-members",
            RoslynCapabilitySurface.Script,
            "Techne.Loom.Abstractions",
            RoslynCapabilityConstraint.None,
            "Use the explicitly approved workflow model member accessors supplied to the script entry point.",
            "tools.script.workflow-model.members",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.StateNode.Id",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.StateNode.Name",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.StateNode.WorkflowPhase",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.StateNode.Groups",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.TransitionGroup.Id",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.TransitionGroup.TransitionIds",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.TransitionBase.Id",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.TransitionBase.Name",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.TransitionBase.Description",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.TransitionBase.WorkflowPhase",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.TransitionBase.TargetNodeId",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.TransitionBase.OutputPath",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.TransitionBase.Priority",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.TransitionBase.StepKind",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.TransitionBase.GuardExpression",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.TransitionBase.SucceedExpression",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.TransitionBase.TerminalRoutes",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.TransitionBase.BlockedRoutes",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.TransitionBase.SatisfiesGateIds",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.TransitionBase.PublishesOutputFamilies",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.TransitionBase.PublishesBlockedOutputFamilies",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.TransitionBase.OwnedInputMode",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.CommandTransition.Command",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.CommandInvocation.Name",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.CommandInvocation.Kind",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.CommandInvocation.Parameters",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.ExpressionDefinition.Kind",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.ExpressionDefinition.Source",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.ExpressionDefinition.ResultType",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowInstance.InstanceId",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowInstance.TemplateKind",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowInstance.RuntimeBinding",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowInstance.RuntimeVersion",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowInstance.StartNodeId",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowInstance.CurrentNodeId",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowInstance.EndNodeId",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowInstance.Status",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowInstance.Context",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowInstance.Nodes",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowInstance.Validation",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowModelReference.SchemaId",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowModelReference.RuntimeBinding",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowValidationContract.Gates",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowValidationContract.Routes",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowValidationContract.ReservedRuntimeOwnedFields",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowValidationGate.Description",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowValidationGate.PassExpression",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowValidationGate.RequiredOutputFamilies",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowValidationGate.ValueSemantics",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowValidationGate.InstanceBinding",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowRouteValidationProfile.RequiredTerminalGateIds",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowRouteValidationProfile.RequiredBlockedGateIds",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowEvidenceReference.StartLine",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowEvidenceReference.EndLine",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowEvidenceReference.Quote",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowGateFailureGuidance.Summary",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowGateFailureGuidance.NextAction",
            "M:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowScriptVerificationSuite.Check(System.String,System.Boolean,System.String,System.String,System.String,System.String,System.String[])",
            "M:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowScriptVerificationSuite.Complete(System.Collections.Generic.IReadOnlyDictionary{System.String,System.Object},System.String[])",
            "F:Techne.Loom.Abstractions.TaskTracking.Model.CommandInvocationKind.Tool",
            "F:Techne.Loom.Abstractions.TaskTracking.Model.CommandInvocationKind.NativeCode",
            "F:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowStepKind.MemoryRead",
            "F:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowStepKind.StateUpdate",
            "F:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowStepKind.MemoryWrite",
            "F:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowStepKind.ToolCall",
            "F:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowStepKind.ModelThink",
            "F:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowStepKind.WaitResume",
            "F:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowStepKind.ConditionBranch",
            "F:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowStepKind.ArtifactEmit",
            "F:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowStatus.ReadyToStart"),
        Exact(
            ScriptSystemObjectModel,
            RoslynCapabilitySurface.Script,
            "System.ObjectModel",
            RoslynCapabilityConstraint.None,
            "Use the explicitly approved object-model collection type.",
            "tools.script.object-model",
            "T:System.Collections.ObjectModel.ReadOnlyCollection`1"),
        Exact(
            ScriptSystemRuntimeExtensions,
            RoslynCapabilitySurface.Script,
            "System.Runtime",
            RoslynCapabilityConstraint.None,
            "Use the explicitly approved runtime metadata type.",
            "tools.script.runtime-extensions",
            "T:System.Runtime.CompilerServices.RuntimeFeature"),
        Exact(
            ScriptLoomModel,
            RoslynCapabilitySurface.Script,
            "Techne.Loom.Abstractions",
            RoslynCapabilityConstraint.None,
            "Use explicitly approved workflow model types and read-only members supplied to the script entry point.",
            "tools.script.workflow-model",
            "T:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowInstance",
            "T:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowScriptInput",
            "T:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowScriptVerificationResult",
            "T:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowModelReference",
            "T:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowValidationContract",
            "T:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowValidationGate",
            "T:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowGateFailureGuidance",
            "T:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowEvidenceReference",
            "T:Techne.Loom.Abstractions.TaskTracking.Model.ExpressionDefinition",
            "T:Techne.Loom.Abstractions.TaskTracking.Model.CommandInvocation",
            "T:Techne.Loom.Abstractions.TaskTracking.Model.CommandTransition",
            "T:Techne.Loom.Abstractions.TaskTracking.Model.StateNode",
            "T:Techne.Loom.Abstractions.TaskTracking.Model.TransitionGroup",
            "T:Techne.Loom.Abstractions.TaskTracking.Model.CommandInvocationKind",
            "T:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowStepKind",
            "T:Techne.Loom.Abstractions.TaskTracking.Model.ConcurrencyStrategy",
            "T:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowScriptVerificationSuite",
            "T:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowScriptVerificationCheck",
            "T:Techne.Loom.Abstractions.TaskTracking.Model.ITaskNode",
            "T:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowRouteValidationProfile",
            "T:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowStatus",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowScriptInput.RuntimeBinding",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowScriptInput.RuntimeVersion",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowScriptInput.Context",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowScriptInput.Options",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowInstance.RuntimeBinding",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowInstance.RuntimeVersion",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowInstance.Context",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowInstance.Nodes",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowInstance.Validation",
            "M:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowInstance.GetStateNodes",
            "M:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowInstance.GetTransitionNodes",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowScriptVerificationResult.Passed",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowValidationContract.Gates",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowValidationGate.FailureGuidance",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowGateFailureGuidance.EvidenceReferences",
            "P:Techne.Loom.Abstractions.TaskTracking.Model.WorkflowEvidenceReference.Path"),
        Exact(
            ScriptRegex,
            RoslynCapabilitySurface.Script,
            "System.Text.RegularExpressions",
            RoslynCapabilityConstraint.RegexMatchTimeout,
            "Use native static Regex matching with an explicit finite timeout of at most five seconds.",
            "tools.script.regex",
            "T:System.Text.RegularExpressions.Regex",
            "T:System.Text.RegularExpressions.RegexOptions",
            "M:System.Text.RegularExpressions.Regex.IsMatch(System.String,System.String,System.Text.RegularExpressions.RegexOptions,System.TimeSpan)",
            "M:System.Text.RegularExpressions.Regex.Match(System.String,System.String,System.Text.RegularExpressions.RegexOptions,System.TimeSpan)",
            "M:System.Text.RegularExpressions.Regex.Matches(System.String,System.String,System.Text.RegularExpressions.RegexOptions,System.TimeSpan)",
            "M:System.Text.RegularExpressions.Regex.Count(System.String,System.String,System.Text.RegularExpressions.RegexOptions,System.TimeSpan)",
            "M:System.Text.RegularExpressions.Regex.Replace(System.String,System.String,System.String,System.Text.RegularExpressions.RegexOptions,System.TimeSpan)",
            "M:System.Text.RegularExpressions.Regex.Split(System.String,System.String,System.Text.RegularExpressions.RegexOptions,System.TimeSpan)",
            "M:System.Text.RegularExpressions.Regex.Escape(System.String)",
            "M:System.Text.RegularExpressions.Regex.Unescape(System.String)",
            "T:System.Text.RegularExpressions.Group",
            "T:System.Text.RegularExpressions.Capture",
            "P:System.Text.RegularExpressions.Capture.Value",
            "P:System.Text.RegularExpressions.Group.Success",
            "T:System.Text.RegularExpressions.Match",
            "T:System.Text.RegularExpressions.MatchCollection",
            "P:System.Text.RegularExpressions.MatchCollection.Count",
            "F:System.Text.RegularExpressions.RegexOptions.None",
            "F:System.Text.RegularExpressions.RegexOptions.CultureInvariant",
            "F:System.Text.RegularExpressions.RegexOptions.IgnoreCase",
            "F:System.Text.RegularExpressions.RegexOptions.Multiline",
            "F:System.Text.RegularExpressions.RegexOptions.Singleline",
            "F:System.Text.RegularExpressions.RegexOptions.ExplicitCapture",
            "F:System.Text.RegularExpressions.RegexOptions.IgnorePatternWhitespace",
            "F:System.Text.RegularExpressions.RegexOptions.NonBacktracking"),
        Exact(
            ScriptJson,
            RoslynCapabilitySurface.Script,
            "System.Text.Json",
            RoslynCapabilityConstraint.ReadOnlyJson,
            "Read JSON values without parsing, serialization, mutation, or unbounded raw extraction.",
            "tools.script.json.read-only",
            "T:System.Text.Json.JsonElement",
            "T:System.Text.Json.JsonValueKind",
            "P:System.Text.Json.JsonElement.ValueKind",
            "P:System.Text.Json.JsonElement.Item(System.Int32)",
            "M:System.Text.Json.JsonElement.TryGetProperty(System.String,System.Text.Json.JsonElement@)",
            "M:System.Text.Json.JsonElement.GetProperty(System.String)",
            "M:System.Text.Json.JsonElement.GetArrayLength",
            "M:System.Text.Json.JsonElement.GetPropertyCount",
            "M:System.Text.Json.JsonElement.GetString",
            "M:System.Text.Json.JsonElement.GetBoolean",
            "M:System.Text.Json.JsonElement.GetInt32",
            "M:System.Text.Json.JsonElement.TryGetInt32(System.Int32@)",
            "M:System.Text.Json.JsonElement.GetInt64",
            "M:System.Text.Json.JsonElement.TryGetInt64(System.Int64@)",
            "M:System.Text.Json.JsonElement.GetDecimal",
            "M:System.Text.Json.JsonElement.TryGetDecimal(System.Decimal@)",
            "M:System.Text.Json.JsonElement.GetDouble",
            "M:System.Text.Json.JsonElement.TryGetDouble(System.Double@)",
            "M:System.Text.Json.JsonElement.GetGuid",
            "M:System.Text.Json.JsonElement.TryGetGuid(System.Guid@)",
            "M:System.Text.Json.JsonElement.GetDateTime",
            "M:System.Text.Json.JsonElement.TryGetDateTime(System.DateTime@)",
            "M:System.Text.Json.JsonElement.GetDateTimeOffset",
            "M:System.Text.Json.JsonElement.TryGetDateTimeOffset(System.DateTimeOffset@)",
            "M:System.Text.Json.JsonElement.EnumerateArray",
            "M:System.Text.Json.JsonElement.EnumerateObject",
            "M:System.Text.Json.JsonElement.ValueEquals(System.String)",
            "M:System.Text.Json.JsonElement.DeepEquals(System.Text.Json.JsonElement,System.Text.Json.JsonElement)",
            "F:System.Text.Json.JsonValueKind.Undefined",
            "F:System.Text.Json.JsonValueKind.Object",
            "F:System.Text.Json.JsonValueKind.Array",
            "F:System.Text.Json.JsonValueKind.String",
            "F:System.Text.Json.JsonValueKind.Number",
            "F:System.Text.Json.JsonValueKind.True",
            "F:System.Text.Json.JsonValueKind.False",
            "F:System.Text.Json.JsonValueKind.Null"),
        Exact(
            ScriptParsing,
            RoslynCapabilitySurface.Script,
            "System.Private.CoreLib",
            RoslynCapabilityConstraint.InvariantParsing,
            "Parse invariant numeric, identifier, date, and duration values with explicit styles and formats.",
            "tools.script.parsing.invariant",
            "M:System.Byte.TryParse(System.String,System.Globalization.NumberStyles,System.IFormatProvider,System.Byte@)",
            "M:System.SByte.TryParse(System.String,System.Globalization.NumberStyles,System.IFormatProvider,System.SByte@)",
            "M:System.Int16.TryParse(System.String,System.Globalization.NumberStyles,System.IFormatProvider,System.Int16@)",
            "M:System.Int32.TryParse(System.String,System.Globalization.NumberStyles,System.IFormatProvider,System.Int32@)",
            "M:System.Int64.TryParse(System.String,System.Globalization.NumberStyles,System.IFormatProvider,System.Int64@)",
            "M:System.UInt16.TryParse(System.String,System.Globalization.NumberStyles,System.IFormatProvider,System.UInt16@)",
            "M:System.UInt32.TryParse(System.String,System.Globalization.NumberStyles,System.IFormatProvider,System.UInt32@)",
            "M:System.UInt64.TryParse(System.String,System.Globalization.NumberStyles,System.IFormatProvider,System.UInt64@)",
            "M:System.Single.TryParse(System.String,System.Globalization.NumberStyles,System.IFormatProvider,System.Single@)",
            "M:System.Double.TryParse(System.String,System.Globalization.NumberStyles,System.IFormatProvider,System.Double@)",
            "M:System.Decimal.TryParse(System.String,System.Globalization.NumberStyles,System.IFormatProvider,System.Decimal@)",
            "M:System.Guid.TryParse(System.String,System.Guid@)",
            "M:System.Guid.TryParse(System.String,System.IFormatProvider,System.Guid@)",
            "M:System.DateTimeOffset.TryParseExact(System.String,System.String,System.IFormatProvider,System.Globalization.DateTimeStyles,System.DateTimeOffset@)",
            "M:System.TimeSpan.TryParseExact(System.String,System.String,System.IFormatProvider,System.Globalization.TimeSpanStyles,System.TimeSpan@)",
            "P:System.Globalization.CultureInfo.InvariantCulture",
            "F:System.Globalization.NumberStyles.None",
            "F:System.Globalization.NumberStyles.Integer",
            "F:System.Globalization.NumberStyles.Number",
            "F:System.Globalization.NumberStyles.Float",
            "F:System.Globalization.NumberStyles.HexNumber",
            "F:System.Globalization.NumberStyles.Any",
            "F:System.Globalization.DateTimeStyles.None",
            "F:System.Globalization.DateTimeStyles.AdjustToUniversal",
            "F:System.Globalization.DateTimeStyles.AssumeUniversal",
            "F:System.Globalization.DateTimeStyles.AllowWhiteSpaces",
            "F:System.Globalization.DateTimeStyles.NoCurrentDateDefault",
            "F:System.Globalization.DateTimeStyles.RoundtripKind",
            "F:System.Globalization.TimeSpanStyles.None",
            "F:System.Globalization.TimeSpanStyles.AssumeNegative"),
        Exact(
            ScriptHashing,
            RoslynCapabilitySurface.Script,
            "System.Security.Cryptography",
            RoslynCapabilityConstraint.InMemoryHashing,
            "Hash in-memory byte arrays with SHA-256 only.",
            "tools.script.hashing.sha256",
            "T:System.Security.Cryptography.SHA256",
            "M:System.Security.Cryptography.SHA256.HashData(System.Byte[])"),
        Exact(
            ScriptEncoding,
            RoslynCapabilitySurface.Script,
            "System.Private.CoreLib",
            RoslynCapabilityConstraint.InMemoryHashing,
            "Use UTF-8 byte conversion in memory.",
            "tools.script.encoding.utf8",
            "T:System.Text.Encoding",
            "P:System.Text.Encoding.UTF8",
            "M:System.Text.Encoding.GetBytes(System.String)",
            "M:System.Text.Encoding.GetString(System.Byte[])"),
        Exact(
            ScriptConversion,
            RoslynCapabilitySurface.Script,
            "System.Private.CoreLib",
            RoslynCapabilityConstraint.InMemoryHashing,
            "Use in-memory hexadecimal and Base64 conversions.",
            "tools.script.encoding.hex-base64",
            "M:System.Convert.ToHexString(System.Byte[])",
            "M:System.Convert.FromHexString(System.String)",
            "M:System.Convert.ToBase64String(System.Byte[])",
            "M:System.Convert.FromBase64String(System.String)"),
    ];

    private static readonly IReadOnlyDictionary<string, RoslynCapabilityDescriptor> byId =
        entries.ToDictionary(static entry => entry.Id, StringComparer.Ordinal);

    public static IReadOnlyList<RoslynCapabilityDescriptor> Entries => entries;

    public static bool TryGet(string capabilityId, out RoslynCapabilityDescriptor descriptor)
        => byId.TryGetValue(capabilityId, out descriptor!);

    public static IEnumerable<RoslynCapabilityDescriptor> FindMatches(ISymbol symbol, RoslynCapabilitySurface surface)
        => entries.Where(entry => entry.Surface == surface && entry.Matches(symbol));

    private static RoslynCapabilityDescriptor Exact(
        string id,
        RoslynCapabilitySurface surface,
        string requiredAssembly,
        RoslynCapabilityConstraint constraint,
        string guidance,
        string documentationId,
        string symbolId,
        params string[] additionalSymbolIds)
        => new(
            id,
            surface,
            RoslynCapabilitySymbolKind.ExactSymbol,
            symbolId,
            requiredAssembly,
            constraint,
            guidance,
            documentationId,
            additionalSymbolIds: additionalSymbolIds);

    private static RoslynCapabilityDescriptor ExactWithAssemblies(
        string id,
        RoslynCapabilitySurface surface,
        string requiredAssembly,
        string alternateAssembly,
        RoslynCapabilityConstraint constraint,
        string guidance,
        string documentationId,
        string symbolId,
        params string[] additionalSymbolIds)
        => new(
            id,
            surface,
            RoslynCapabilitySymbolKind.ExactSymbol,
            symbolId,
            requiredAssembly,
            constraint,
            guidance,
            documentationId,
            additionalSymbolIds: additionalSymbolIds,
            additionalAssemblies: [alternateAssembly]);

    private static RoslynCapabilityDescriptor AssemblyFamily(
        string id,
        string requiredAssembly,
        string namespacePrefix,
        string guidance,
        string documentationId)
        => new(
            id,
            RoslynCapabilitySurface.Script,
            RoslynCapabilitySymbolKind.AssemblyFamily,
            $"A:{requiredAssembly}",
            requiredAssembly,
            RoslynCapabilityConstraint.None,
            guidance,
            documentationId,
            namespacePrefix);
}
