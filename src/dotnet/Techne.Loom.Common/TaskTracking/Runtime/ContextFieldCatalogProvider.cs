using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public sealed record ContextFieldCatalogReadResult(
    string ContractPath,
    string ContractSha256,
    DateTimeOffset ReadAtUtc,
    bool CacheHit,
    ContextFieldCatalog Catalog,
    int ReturnedBytes,
    WorkflowFragmentLimits Limits);

public sealed class ContextFieldCatalogProvider
{
    private readonly ContractContextProvider _contractProvider;

    public ContextFieldCatalogProvider(ContractContextProvider? contractProvider = null)
    {
        _contractProvider = contractProvider ?? new ContractContextProvider();
    }

    public async Task<ContextFieldCatalogReadResult> ReadAsync(
        ContractBinding binding,
        string? assetRoot = null,
        WorkflowFragmentLimits? limits = null,
        CancellationToken ct = default)
    {
var effectiveLimits = limits ?? WorkflowFragmentLimits.Default with { MaxArrayItems = 512, MaxObjectProperties = 256 };
        var result = await _contractProvider.ReadAsync(
            binding,
            ["/context_fields"],
            assetRoot,
            effectiveLimits,
            ct).ConfigureAwait(false);
        var fragment = result.Fragments["/context_fields"];
        var wrapper = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["context_fields"] = fragment,
        };
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(wrapper));
        var catalog = ContextFieldCatalog.Parse(document.RootElement, result.ContractPath);

        return new ContextFieldCatalogReadResult(
            result.ContractPath,
            result.ContractSha256,
            result.ReadAtUtc,
            result.CacheHit,
            catalog,
            result.ReturnedBytes,
            result.Limits);
    }
}
