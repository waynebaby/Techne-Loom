using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public sealed record ContractContextReadResult(
    string ContractPath,
    string ContractSha256,
    DateTimeOffset ReadAtUtc,
    bool CacheHit,
    IReadOnlyDictionary<string, JsonElement> Fragments,
    int ReturnedBytes,
    WorkflowFragmentLimits Limits);

public sealed class ContractContextProvider
{
    private readonly ConcurrentDictionary<string, CachedContract> _cache = new(StringComparer.OrdinalIgnoreCase);

    public async Task<ContractContextReadResult> ReadAsync(
        ContractBinding binding,
        IEnumerable<string> contractRefs,
        string? assetRoot = null,
        WorkflowFragmentLimits? limits = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(contractRefs);

        if (!string.Equals(binding.Format, "json", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Contract format '{binding.Format}' is not supported. Only JSON contracts are supported.");
        }

        var effectiveLimits = limits ?? WorkflowFragmentLimits.Default;
        effectiveLimits.Validate();
        var contractPath = ResolveContractPath(binding.Path, assetRoot);
        var contract = await ReadSnapshotAsync(contractPath, effectiveLimits, ct).ConfigureAwait(false);
        var references = contractRefs
            .Where(static reference => !string.IsNullOrWhiteSpace(reference))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var fragments = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        var returnedBytes = 0;

        foreach (var reference in references)
        {
            var fragment = ResolveJsonPointer(contract.Root, reference);
            EnsureWithinLimits(fragment, reference, effectiveLimits);
            fragments[reference] = fragment.Clone();
            returnedBytes += Encoding.UTF8.GetByteCount(fragment.GetRawText());
        }

        return new ContractContextReadResult(
            contractPath,
            contract.Sha256,
            DateTimeOffset.UtcNow,
            contract.CacheHit,
            fragments,
            returnedBytes,
            effectiveLimits);
    }

    private async Task<CachedContract> ReadSnapshotAsync(
        string contractPath,
        WorkflowFragmentLimits limits,
        CancellationToken ct)
    {
        await using var contractLock = await WorkflowFileLock.AcquireAsync(contractPath, ct).ConfigureAwait(false);
        var bytes = await File.ReadAllBytesAsync(contractPath, ct).ConfigureAwait(false);
        var sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if (_cache.TryGetValue(contractPath, out var cached)
            && string.Equals(cached.Sha256, sha256, StringComparison.Ordinal))
        {
            return cached with { CacheHit = true };
        }

        using var document = JsonDocument.Parse(bytes);
        ValidateContractShape(document.RootElement, contractPath);
        var snapshot = new CachedContract(contractPath, sha256, document.RootElement.Clone(), CacheHit: false);
        _cache[contractPath] = snapshot;
        return snapshot;
    }

    private static string ResolveContractPath(string contractPath, string? assetRoot)
    {
        if (string.IsNullOrWhiteSpace(contractPath))
        {
            throw new InvalidOperationException("Contract binding path is required.");
        }

        var normalizedRoot = Path.GetFullPath(assetRoot ?? Directory.GetCurrentDirectory());
        var normalizedPath = Path.GetFullPath(Path.IsPathRooted(contractPath)
            ? contractPath
            : Path.Combine(normalizedRoot, contractPath));
        if (!File.Exists(normalizedPath))
        {
            throw new FileNotFoundException("The contract file was not found.", normalizedPath);
        }

        if (!IsWithinRoot(normalizedRoot, normalizedPath))
        {
            throw new InvalidOperationException($"Contract path '{contractPath}' escapes the declared asset root '{normalizedRoot}'.");
        }

        return normalizedPath;
    }

    private static bool IsWithinRoot(string root, string candidate)
    {
        var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar;
        var normalizedCandidate = Path.GetFullPath(candidate);
        return normalizedCandidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateContractShape(JsonElement root, string contractPath)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"Contract '{contractPath}' must contain a JSON object.");
        }

        if (!root.TryGetProperty("name", out var name)
            || name.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(name.GetString()))
        {
            throw new InvalidOperationException($"Contract '{contractPath}' must contain a non-empty string 'name'.");
        }

        foreach (var propertyName in new[] { "inputs", "outputs", "default_assumptions" })
        {
            if (!root.TryGetProperty(propertyName, out var property)
                || property.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException($"Contract '{contractPath}' must contain an object '{propertyName}'.");
            }
        }
    }

    private static void EnsureWithinLimits(
        JsonElement element,
        string reference,
        WorkflowFragmentLimits limits,
        int depth = 0)
    {
        if (depth > limits.MaxDepth)
        {
            throw new InvalidOperationException($"Contract fragment '{reference}' exceeds max depth {limits.MaxDepth}.");
        }

        if (Encoding.UTF8.GetByteCount(element.GetRawText()) > limits.MaxBytes)
        {
            throw new InvalidOperationException($"Contract fragment '{reference}' exceeds max bytes {limits.MaxBytes}.");
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            var properties = element.EnumerateObject().ToArray();
            if (properties.Length > limits.MaxObjectProperties)
            {
                throw new InvalidOperationException($"Contract fragment '{reference}' exceeds max object properties {limits.MaxObjectProperties}.");
            }

            foreach (var property in properties)
            {
                EnsureWithinLimits(property.Value, reference, limits, depth + 1);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            var items = element.EnumerateArray().ToArray();
            if (items.Length > limits.MaxArrayItems)
            {
                throw new InvalidOperationException($"Contract fragment '{reference}' exceeds max array items {limits.MaxArrayItems}.");
            }

            foreach (var item in items)
            {
                EnsureWithinLimits(item, reference, limits, depth + 1);
            }
        }
    }

    private static JsonElement ResolveJsonPointer(JsonElement root, string pointer)
    {
        if (string.IsNullOrEmpty(pointer))
        {
            return root;
        }

        if (!pointer.StartsWith('/'))
        {
            throw new ArgumentException("Contract references must be empty or start with '/'.", nameof(pointer));
        }

        var current = root;
        foreach (var rawSegment in pointer.Split('/').Skip(1))
        {
            if (rawSegment.Contains('~'))
            {
                for (var index = 0; index < rawSegment.Length; index++)
                {
                    if (rawSegment[index] == '~'
                        && (index + 1 >= rawSegment.Length
                            || rawSegment[index + 1] is not ('0' or '1')))
                    {
                        throw new InvalidOperationException($"Contract reference '{pointer}' contains an invalid escape sequence.");
                    }
                }
            }
            var segment = rawSegment.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
            if (current.ValueKind == JsonValueKind.Object)
            {
                if (!current.TryGetProperty(segment, out current))
                {
                    throw new InvalidOperationException($"Contract reference '{pointer}' was not found.");
                }
            }
            else if (current.ValueKind == JsonValueKind.Array)
            {
                if (!TryParseJsonArrayIndex(segment, out var index))
                {
                    throw new InvalidOperationException($"Contract reference '{pointer}' contains an invalid array index '{segment}'.");
                }

                if (index >= current.GetArrayLength())
                {
                    throw new InvalidOperationException($"Contract reference '{pointer}' cannot descend through '{segment}'.");
                }

                current = current[index];
            }
            else
            {
                throw new InvalidOperationException($"Contract reference '{pointer}' cannot descend through '{segment}'.");
            }
        }

        return current;
    }

    private static bool TryParseJsonArrayIndex(string segment, out int index)
    {
        index = -1;
        if (string.IsNullOrEmpty(segment)
            || (segment.Length > 1 && segment[0] == '0')
            || !segment.All(static character => character is >= '0' and <= '9'))
        {
            return false;
        }

        return int.TryParse(segment, out index);
    }

    private sealed record CachedContract(
        string ContractPath,
        string Sha256,
        JsonElement Root,
        bool CacheHit);
}
