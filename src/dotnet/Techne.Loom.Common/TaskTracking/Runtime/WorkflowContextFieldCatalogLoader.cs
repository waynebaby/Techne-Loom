using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public static class WorkflowContextFieldCatalogLoader
{
    public static Task<ContextFieldCatalogReadResult> ReadRequiredAsync(
        WorkflowInstance instance,
        WorkflowFragmentLimits? limits = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(instance);

        var binding = instance.ContractBinding
            ?? throw new InvalidOperationException("The workflow must declare contractBinding for its owning context_fields catalog.");
        var configuredRoot = string.IsNullOrWhiteSpace(binding.AssetRootPath)
            ? null
            : binding.AssetRootPath;
        var inputRoot = ReadAssetRootInput(instance, binding.AssetRootInput);
        if (configuredRoot is not null && inputRoot is not null
            && !string.Equals(
                Path.GetFullPath(configuredRoot),
                Path.GetFullPath(inputRoot),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            throw new InvalidOperationException("contractBinding.assetRootPath and assetRootInput resolve to different owning skill roots.");
        }

        var assetRoot = configuredRoot ?? inputRoot
            ?? throw new InvalidOperationException("contractBinding must identify the owning skill root with assetRootPath or a string-valued assetRootInput.");
        return new ContextFieldCatalogProvider().ReadAsync(binding, assetRoot, limits, ct);
    }

    private static string? ReadAssetRootInput(WorkflowInstance instance, string? assetRootInput)
    {
        if (string.IsNullOrWhiteSpace(assetRootInput))
        {
            return null;
        }

        var value = PathValueAccessor.GetValue(instance.Context, assetRootInput);
        var path = value switch
        {
            string text => text,
            JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
            _ => null,
        };
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException($"contractBinding.assetRootInput '{assetRootInput}' must resolve to a non-empty string path.");
        }

        return path;
    }
}
