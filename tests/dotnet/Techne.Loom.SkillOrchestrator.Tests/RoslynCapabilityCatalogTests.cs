using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class RoslynCapabilityCatalogTests
{
    [Fact]
    public void EntriesHaveUniqueStableIdentifiersAndDocumentationKeys()
    {
        var entries = RoslynCapabilityCatalog.Entries;

        Assert.NotEmpty(entries);
        Assert.Equal(entries.Count, entries.Select(static entry => entry.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(entries.Count, entries.Select(static entry => entry.DocumentationId).Distinct(StringComparer.Ordinal).Count());
        Assert.All(entries, entry =>
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.Id));
            Assert.False(string.IsNullOrWhiteSpace(entry.SymbolId));
            Assert.False(string.IsNullOrWhiteSpace(entry.RequiredAssembly));
            Assert.False(string.IsNullOrWhiteSpace(entry.DiagnosticGuidance));
            Assert.False(string.IsNullOrWhiteSpace(entry.DocumentationId));
            Assert.DoesNotContain('*', entry.SymbolId);
            Assert.DoesNotContain('*', entry.NamespacePrefix ?? string.Empty);
            Assert.Equal(entry.SymbolIds.Length, entry.SymbolIds.Distinct(StringComparer.Ordinal).Count());
        });
    }

    [Fact]
    public void ScriptCapabilitiesDoNotUseAssemblyOrNamespaceWildcards()
    {
        Assert.DoesNotContain(
            RoslynCapabilityCatalog.Entries,
            entry => entry.Surface == RoslynCapabilitySurface.Script
                && entry.SymbolKind == RoslynCapabilitySymbolKind.AssemblyFamily);
        Assert.All(
            RoslynCapabilityCatalog.Entries.Where(static entry => entry.Surface == RoslynCapabilitySurface.Script),
            entry => Assert.All(entry.SymbolIds, symbolId => Assert.DoesNotContain('*', symbolId)));
    }

    [Fact]
    public void RequiredAssembliesArePresentInTheCurrentRuntime()
    {
        var availableAssemblies = new HashSet<string>(
            AppDomain.CurrentDomain.GetAssemblies()
                .Select(static assembly => assembly.GetName().Name)
                .Where(static name => !string.IsNullOrWhiteSpace(name))!,
            StringComparer.Ordinal);
        var trustedAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")
            ?? throw new InvalidOperationException("The .NET trusted platform assembly list is unavailable.");
        foreach (var path in trustedAssemblies.Split(Path.PathSeparator))
        {
            var name = Path.GetFileNameWithoutExtension(path);
            if (!string.IsNullOrWhiteSpace(name))
            {
                availableAssemblies.Add(name);
            }
        }

        Assert.All(
            RoslynCapabilityCatalog.Entries,
            entry => Assert.All(entry.RequiredAssemblies, assembly => Assert.Contains(assembly, availableAssemblies)));
    }

    [Fact]
    public void ContextGetGenericMethodResolvesToTheBaselineCapability()
    {
        var source = "class S { bool Evaluate(Techne.Loom.Common.TaskTracking.Runtime.ExpressionRuntimeContext context) => context.Get<int>(\"score\") > 0; }";
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            "RoslynCapabilityCatalogTests",
            [syntaxTree],
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(ExpressionRuntimeContext).Assembly.Location),
            ],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var invocation = syntaxTree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Single();
        var symbol = compilation.GetSemanticModel(syntaxTree).GetSymbolInfo(invocation).Symbol;

        Assert.NotNull(symbol);
        var matches = RoslynCapabilityCatalog.FindMatches(symbol!, RoslynCapabilitySurface.Expression).ToArray();
        Assert.Contains(matches, entry => entry.Id == RoslynCapabilityCatalog.ExpressionContextGet);
    }

    [Fact]
    public void ExactCatalogSymbolsResolveFromTheCurrentTrustedPlatformSet()
    {
        var trustedAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")
            ?? throw new InvalidOperationException("The .NET trusted platform assembly list is unavailable.");
        var references = trustedAssemblies
            .Split(Path.PathSeparator)
            .Where(File.Exists)
            .Select(static path => MetadataReference.CreateFromFile(path))
            .Append(MetadataReference.CreateFromFile(typeof(ExpressionRuntimeContext).Assembly.Location))
            .ToArray();
        var compilation = CSharpCompilation.Create("RoslynCapabilityCatalogSymbolTests", references: references);

        foreach (var entry in RoslynCapabilityCatalog.Entries.Where(static entry => entry.SymbolKind == RoslynCapabilitySymbolKind.ExactSymbol))
        {
            foreach (var symbolId in entry.SymbolIds)
            {
                var symbol = DocumentationCommentId.GetFirstSymbolForDeclarationId(symbolId, compilation);
                Assert.True(symbol is not null, $"{entry.Id}: {symbolId}");
            }
        }
    }
}
