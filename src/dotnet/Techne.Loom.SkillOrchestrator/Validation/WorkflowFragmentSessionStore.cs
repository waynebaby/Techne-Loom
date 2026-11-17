using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.SkillOrchestrator.Validation;

internal sealed class WorkflowFragmentSessionStore
{
    private const string DirectoryPrefix = "loom-fragment-session-";
    private const string SessionSchema = "loom.workflow-fragment-session.v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
    };

    private WorkflowFragmentSessionStore(string root)
    {
        Root = root;
    }

    internal string Root { get; }

    private string Head => Path.Combine(Root, "head");

    internal static async Task<(WorkflowFragmentSessionStore Store, WorkflowFragmentSnapshot Snapshot)> CreateAsync(
        string baselineFile,
        IEnumerable<WorkflowFragmentId> authorizedFragments,
        CancellationToken cancellationToken = default)
    {
        var sourcePath = Path.GetFullPath(baselineFile);
        var sourceJson = await File.ReadAllTextAsync(sourcePath, cancellationToken).ConfigureAwait(false);
        var draft = WorkflowDraftImport.Read(sourceJson);
        var catalog = await WorkflowContextFieldCatalogLoader.ReadRequiredAsync(draft, ct: cancellationToken).ConfigureAwait(false);
        var scope = authorizedFragments.ToImmutableArray();
        var session = await WorkflowFragmentDesignSession.StartAsync(sourceJson, scope, cancellationToken, catalog.Catalog)
            .ConfigureAwait(false);
        var inputs = new DesignInputs(sourcePath, Digest(sourceJson), draft.InstanceId, draft.RuntimeVersion!,
            catalog.ContractPath, catalog.ContractSha256);
        await VerifyInputsAsync(inputs, cancellationToken).ConfigureAwait(false);

        var directory = Directory.CreateTempSubdirectory(DirectoryPrefix);
        var store = new WorkflowFragmentSessionStore(directory.FullName);
        await store.PublishAsync(inputs, scope, session.Current, cancellationToken).ConfigureAwait(false);
        return (store, session.Current);
    }

    internal static WorkflowFragmentSessionStore Open(string path)
    {
        var directory = new DirectoryInfo(Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)));
        var tempRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!string.Equals(directory.Parent?.FullName, tempRoot, comparison)
            || !directory.Name.StartsWith(DirectoryPrefix, StringComparison.Ordinal)
            || !directory.Exists
            || directory.Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw new InvalidOperationException("Expected an existing private Loom fragment session under the system temporary directory.");
        }

        if (!OperatingSystem.IsWindows()
            && (File.GetUnixFileMode(directory.FullName) & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute)) != 0)
        {
            throw new InvalidOperationException("The workflow design session directory must be private to its owner.");
        }

        return new WorkflowFragmentSessionStore(directory.FullName);
    }

    internal async Task<WorkflowFragmentSnapshot> ReadAsync(CancellationToken cancellationToken = default)
    {
        await using var readLock = await AcquireLockAsync(cancellationToken).ConfigureAwait(false);
        var state = await ReadStateAsync(cancellationToken).ConfigureAwait(false);
        await VerifyInputsAsync(state.Inputs, cancellationToken).ConfigureAwait(false);
        return Restore(state).Current;
    }

    internal async Task<WorkflowFragmentSnapshot> ApplyAsync(
        long generation,
        WorkflowFragmentEdit edit,
        string reason,
        CancellationToken cancellationToken = default)
    {
        await using var writeLock = await AcquireLockAsync(cancellationToken).ConfigureAwait(false);
        var state = await ReadStateAsync(cancellationToken).ConfigureAwait(false);
        var catalog = await VerifyInputsAsync(state.Inputs, cancellationToken).ConfigureAwait(false);
        var session = Restore(state);
        var next = await session.ApplyAsync(generation, edit, reason, cancellationToken, catalog.Catalog).ConfigureAwait(false);
        if (next.Generation != state.Generation)
        {
            await VerifyInputsAsync(state.Inputs, cancellationToken).ConfigureAwait(false);
            await PublishAsync(state.Inputs, state.AuthorizedFragments, next, cancellationToken).ConfigureAwait(false);
        }

        return next;
    }

    private async Task<DesignState> ReadStateAsync(CancellationToken cancellationToken)
    {
        var digest = await ReadSessionFileAsync(Head, cancellationToken).ConfigureAwait(false);
        if (digest.Length != 64 || !digest.All(Uri.IsHexDigit))
        {
            throw new InvalidOperationException("The workflow design session head is corrupted.");
        }

        var json = await ReadSessionFileAsync(Path.Combine(Root, digest + ".json"), cancellationToken).ConfigureAwait(false);
        if (!string.Equals(Digest(json), digest, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The workflow design snapshot checksum does not match its committed head.");
        }

        var state = JsonSerializer.Deserialize<DesignState>(json, JsonOptions);
        if (state is null || state.Schema != SessionSchema
            || state.Fragments.Any(entry => entry is null || entry.Revision is null))
        {
            throw new InvalidOperationException("The workflow design session schema is unsupported.");
        }

        return state;
    }

    private static WorkflowFragmentDesignSession Restore(DesignState state)
    {
        var workflow = WorkflowDraftImport.Read(state.WorkflowJson);
        if (workflow.InstanceId != state.Inputs.InstanceId
            || workflow.RuntimeBinding != "dotnet-so"
            || workflow.RuntimeVersion != state.Inputs.RuntimeVersion)
        {
            throw new InvalidOperationException("The workflow design snapshot identity does not match its baseline.");
        }

        var snapshot = new WorkflowFragmentSnapshot(state.Generation, state.WorkflowJson,
            state.Fragments.ToImmutableDictionary(item => item.Id, item => item.Revision), state.LockEvents);
        return WorkflowFragmentDesignSession.Restore(state.AuthorizedFragments, snapshot);
    }

    private async Task PublishAsync(
        DesignInputs inputs,
        ImmutableArray<WorkflowFragmentId> authorizedFragments,
        WorkflowFragmentSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var fragments = snapshot.Fragments.Select(item => new FragmentEntry(item.Key, item.Value)).ToArray();
        var state = new DesignState(SessionSchema, inputs, authorizedFragments, snapshot.Generation,
            snapshot.WorkflowJson, fragments, snapshot.LockEvents);
        var json = JsonSerializer.Serialize(state, JsonOptions);
        var digest = Digest(json);
        var snapshotPath = Path.Combine(Root, digest + ".json");
        if (!File.Exists(snapshotPath))
        {
            await PublishFileAsync(snapshotPath, json, overwrite: false, cancellationToken).ConfigureAwait(false);
        }
        else if (await ReadSessionFileAsync(snapshotPath, cancellationToken).ConfigureAwait(false) != json)
        {
            throw new InvalidOperationException("A saved workflow design snapshot is corrupted.");
        }

        await PublishFileAsync(Head, digest, overwrite: true, cancellationToken).ConfigureAwait(false);
    }

    private Task<IAsyncDisposable> AcquireLockAsync(CancellationToken cancellationToken)
    {
        Open(Root);
        RejectLink(Head + ".lock");
        return WorkflowFileLock.AcquireAsync(Head, cancellationToken);
    }

    private static Task<string> ReadSessionFileAsync(string path, CancellationToken cancellationToken)
    {
        RejectLink(path);
        return File.ReadAllTextAsync(path, cancellationToken);
    }

    private static void RejectLink(string path)
    {
        var file = new FileInfo(path);
        if (file.LinkTarget is not null)
        {
            throw new InvalidOperationException("The workflow design session contains an unexpected file link.");
        }
    }

    private static async Task PublishFileAsync(string path, string text, bool overwrite, CancellationToken cancellationToken)
    {
        var staging = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(staging, text, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(staging, path, overwrite);
        }
        finally
        {
            File.Delete(staging);
        }
    }

    private static async Task<ContextFieldCatalogReadResult> VerifyInputsAsync(DesignInputs inputs, CancellationToken cancellationToken)
    {
        if (inputs.RuntimeVersion != SkillCli.GetRuntimeVersion())
        {
            throw new InvalidOperationException("The SO version differs from the workflow design session. Start a new session.");
        }

        var baseline = await File.ReadAllTextAsync(inputs.BaselinePath, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(Digest(baseline), inputs.BaselineSha256, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The source workflow changed. Import the new baseline into a fresh session.");
        }

        var catalog = await WorkflowContextFieldCatalogLoader.ReadRequiredAsync(
            WorkflowDraftImport.Read(baseline), ct: cancellationToken).ConfigureAwait(false);
        if (catalog.ContractPath != inputs.ContractPath || catalog.ContractSha256 != inputs.ContractSha256)
        {
            throw new InvalidOperationException("The owning context catalog changed. Start a new workflow design session.");
        }

        return catalog;
    }

    private static string Digest(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private sealed record DesignInputs(
        string BaselinePath, string BaselineSha256, string InstanceId, string RuntimeVersion,
        string ContractPath, string ContractSha256);

    private sealed record FragmentEntry(WorkflowFragmentId Id, WorkflowFragmentRevision Revision);

    private sealed record DesignState(
        string Schema,
        DesignInputs Inputs,
        ImmutableArray<WorkflowFragmentId> AuthorizedFragments,
        long Generation,
        string WorkflowJson,
        FragmentEntry[] Fragments,
        ImmutableArray<WorkflowFragmentLockEvent> LockEvents);
}
