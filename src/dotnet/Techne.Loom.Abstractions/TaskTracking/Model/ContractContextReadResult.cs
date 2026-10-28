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
