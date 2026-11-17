using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Techne.Loom.Common.TaskTracking.Runtime;

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

    private readonly WorkflowFragmentRuntime _runtime;

    private WorkflowFragmentSessionStore(string root, WorkflowFragmentRuntime runtime)
    {
        Root = root;
        _runtime = runtime;
    }

    internal string Root { get; }

    private string Head => Path.Combine(Root, "head");

    internal static async Task<(WorkflowFragmentSessionStore Store, WorkflowFragmentSnapshot Snapshot)> CreateAsync(
        string baselineFile,
        IEnumerable<WorkflowFragmentId> authorizedFragments,
        WorkflowFragmentRuntime runtime,
        CancellationToken cancellationToken = default)
    {
        var sourcePath = Path.GetFullPath(baselineFile);
        var sourceJson = await File.ReadAllTextAsync(sourcePath, cancellationToken).ConfigureAwait(false);
        var draft = WorkflowDraftImport.Read(sourceJson);
        runtime.CheckIdentity(draft);
        var catalog = await WorkflowContextFieldCatalogLoader.ReadRequiredAsync(draft, ct: cancellationToken).ConfigureAwait(false);
        var scope = authorizedFragments.ToImmutableArray();
        var session = await WorkflowFragmentDesignSession.StartAsync(sourceJson, scope, runtime, cancellationToken, catalog.Catalog)
            .ConfigureAwait(false);
        var inputs = new DesignInputs(sourcePath, Digest(sourceJson), draft.InstanceId, draft.RuntimeVersion!,
            catalog.ContractPath, catalog.ContractSha256, runtime.ValidationIdentity);
        var directory = Directory.CreateTempSubdirectory(DirectoryPrefix);
        var store = new WorkflowFragmentSessionStore(directory.FullName, runtime);
        await store.VerifyInputsAsync(inputs, cancellationToken).ConfigureAwait(false);
        await store.PublishAsync(inputs, scope, session.Current, cancellationToken).ConfigureAwait(false);
        return (store, session.Current);
    }

    internal static WorkflowFragmentSessionStore Open(string path, WorkflowFragmentRuntime runtime)
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

        return new WorkflowFragmentSessionStore(directory.FullName, runtime);
    }

    internal async Task<WorkflowFragmentSnapshot> ReadAsync(CancellationToken cancellationToken = default)
        => (await ReadForCompositionAsync(cancellationToken).ConfigureAwait(false)).Snapshot;

    internal async Task<(long Generation, string BaselinePath, string HandoffPath, IReadOnlyList<WorkflowFragmentId> Scope)> WritePromptHandoffAsync()
    {
        await using var readLock = await AcquireLockAsync(CancellationToken.None).ConfigureAwait(false);
        var state = await ReadStateAsync(CancellationToken.None).ConfigureAwait(false);
        await VerifyInputsAsync(state.Inputs, CancellationToken.None).ConfigureAwait(false);
        Restore(state);
        using var workflow = JsonDocument.Parse(state.WorkflowJson);
        var handoff = JsonSerializer.Serialize(new
        {
            schema = "loom.fragment-design-handoff.v1",
            generation = state.Generation,
            workflow = workflow.RootElement,
            authorizedFragments = state.AuthorizedFragments,
        }, JsonOptions);
        var path = Path.Combine(Root, "handoff-" + Digest(handoff) + ".json");
        if (!File.Exists(path))
        {
            await PublishFileAsync(path, handoff, overwrite: false, CancellationToken.None).ConfigureAwait(false);
        }
        else if (await ReadSessionFileAsync(path, CancellationToken.None).ConfigureAwait(false) != handoff)
        {
            throw new InvalidOperationException("The saved workflow design handoff is corrupted.");
        }

        return (state.Generation, state.Inputs.BaselinePath, path, state.AuthorizedFragments);
    }

    internal async Task<(WorkflowFragmentSnapshot Snapshot, ContextFieldCatalog Catalog)> ReadForCompositionAsync(
        CancellationToken cancellationToken = default)
    {
        await using var readLock = await AcquireLockAsync(cancellationToken).ConfigureAwait(false);
        var state = await ReadStateAsync(cancellationToken).ConfigureAwait(false);
        var catalog = await VerifyInputsAsync(state.Inputs, cancellationToken).ConfigureAwait(false);
        return (Restore(state).Current, catalog.Catalog);
    }

    internal async Task PublishCandidateAsync(
        string staging, string destination, long expectedGeneration, CancellationToken cancellationToken = default)
    {
        await using var writeLock = await AcquireLockAsync(cancellationToken).ConfigureAwait(false);
        var state = await ReadStateAsync(cancellationToken).ConfigureAwait(false);
        await VerifyInputsAsync(state.Inputs, cancellationToken).ConfigureAwait(false);
        if (state.Generation != expectedGeneration)
        {
            throw new InvalidOperationException("The session changed during compile. Compose its current generation.");
        }

        var content = await ReadSessionFileAsync(staging, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(content, state.WorkflowJson, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The compiled candidate does not match the current design generation.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        File.Move(staging, destination);
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
        var reuse = state.Inputs.ValidationIdentity is { } recordedIdentity && recordedIdentity == _runtime.ValidationIdentity;
        var next = await session.ApplyAsync(generation, edit, reason, cancellationToken, catalog.Catalog, reuse).ConfigureAwait(false);
        if (next.Generation != state.Generation)
        {
            await VerifyInputsAsync(state.Inputs, cancellationToken).ConfigureAwait(false);
            await PublishAsync(state.Inputs with { ValidationIdentity = _runtime.ValidationIdentity },
                state.AuthorizedFragments, next, cancellationToken).ConfigureAwait(false);
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

    private WorkflowFragmentDesignSession Restore(DesignState state)
    {
        var workflow = WorkflowDraftImport.Read(state.WorkflowJson);
        if (workflow.InstanceId != state.Inputs.InstanceId
            || workflow.RuntimeBinding != _runtime.Binding
            || workflow.RuntimeVersion != state.Inputs.RuntimeVersion)
        {
            throw new InvalidOperationException("The workflow design snapshot identity does not match its baseline.");
        }

        var snapshot = new WorkflowFragmentSnapshot(state.Generation, state.WorkflowJson,
            state.Fragments.ToImmutableDictionary(item => item.Id, item => item.Revision), state.LockEvents);
        return WorkflowFragmentDesignSession.Restore(state.AuthorizedFragments, snapshot, _runtime);
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
        Open(Root, _runtime);
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

    private async Task<ContextFieldCatalogReadResult> VerifyInputsAsync(DesignInputs inputs, CancellationToken cancellationToken)
    {
        if (inputs.RuntimeVersion != _runtime.Version)
        {
            throw new InvalidOperationException("The runtime version differs from the workflow design session. Start a new session.");
        }

        var baseline = await File.ReadAllTextAsync(inputs.BaselinePath, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(Digest(baseline), inputs.BaselineSha256, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The source workflow changed. Import the new baseline into a fresh session.");
        }

        var draft = WorkflowDraftImport.Read(baseline);
        _runtime.CheckIdentity(draft);
        var catalog = await WorkflowContextFieldCatalogLoader.ReadRequiredAsync(draft, ct: cancellationToken).ConfigureAwait(false);
        if (catalog.ContractPath != inputs.ContractPath || catalog.ContractSha256 != inputs.ContractSha256)
        {
            throw new InvalidOperationException("The owning context catalog changed. Start a new workflow design session.");
        }

        return catalog;
    }

    private static string Digest(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private sealed record DesignInputs(
        string BaselinePath, string BaselineSha256, string InstanceId, string RuntimeVersion,
        string ContractPath, string ContractSha256, string? ValidationIdentity = null);

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
