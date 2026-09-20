namespace Techne.Loom.Abstractions.TaskTracking.Model;

public sealed class ContractBinding
{
    public string Path { get; init; } = "assets/so-workflow/contract.json";

    public string Format { get; init; } = "json";

    public string? AssetRootInput { get; init; }

    public string? AssetRootPath { get; init; }
}
