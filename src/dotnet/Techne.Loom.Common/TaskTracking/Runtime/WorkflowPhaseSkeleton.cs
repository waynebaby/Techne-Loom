using System.Text.Json;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public sealed record WorkflowPhaseDefinition(
    string Id,
    string Prefix,
    string Mode,
    IReadOnlyList<string> Entries,
    IReadOnlyList<string> Exits,
    IReadOnlyList<string> Imports,
    IReadOnlyList<string> Exports,
    IReadOnlyList<string> Dependencies,
    IReadOnlyList<string> GateIds,
    IReadOnlyList<string> TerminalRoutes);

public sealed record WorkflowPhaseEdge(
    string FromPhaseId,
    string FromExitId,
    string ToPhaseId,
    string ToEntryId);

public sealed class WorkflowPhaseSkeleton
{
    public const string CurrentSchemaVersion = "loom.workflow-skeleton.v1";

    private WorkflowPhaseSkeleton(
        string workflowId,
        IReadOnlyList<WorkflowPhaseDefinition> phases,
        IReadOnlyList<WorkflowPhaseEdge> routes,
        IReadOnlyList<WorkflowPhaseEdge> backedges)
    {
        WorkflowId = workflowId;
        Phases = phases;
        Routes = routes;
        Backedges = backedges;
    }

    public string SchemaVersion => CurrentSchemaVersion;

    public string WorkflowId { get; }

    public IReadOnlyList<WorkflowPhaseDefinition> Phases { get; }

    public IReadOnlyList<WorkflowPhaseEdge> Routes { get; }

    public IReadOnlyList<WorkflowPhaseEdge> Backedges { get; }

    public static WorkflowPhaseSkeleton Parse(JsonElement root, string sourcePath)
    {
        EnsureObjectProperties(
            root,
            sourcePath,
            "root",
            "schemaVersion",
            "workflowId",
            "phases",
            "routes",
            "backedges");

        var schemaVersion = ReadRequiredString(root, "schemaVersion", sourcePath, "root");
        if (!string.Equals(schemaVersion, CurrentSchemaVersion, StringComparison.Ordinal))
        {
            throw Invalid(sourcePath, $"schemaVersion '{schemaVersion}' is unsupported; expected '{CurrentSchemaVersion}'.");
        }

        var workflowId = ReadRequiredString(root, "workflowId", sourcePath, "root");
        if (!root.TryGetProperty("phases", out var phasesElement)
            || phasesElement.ValueKind != JsonValueKind.Array
            || phasesElement.GetArrayLength() == 0)
        {
            throw Invalid(sourcePath, "phases must be a non-empty array.");
        }

        var phases = phasesElement.EnumerateArray()
            .Select((phase, index) => ReadPhase(phase, sourcePath, $"phases[{index}]"))
            .ToArray();
        var phaseById = new Dictionary<string, WorkflowPhaseDefinition>(StringComparer.Ordinal);
        foreach (var phase in phases)
        {
            if (!phaseById.TryAdd(phase.Id, phase))
            {
                throw Invalid(sourcePath, $"Phase id '{phase.Id}' is duplicated.");
            }
        }

        var routes = ReadEdges(root, "routes", sourcePath);
        var backedges = ReadEdges(root, "backedges", sourcePath);
        ValidatePhaseReferences(phases, phaseById, routes, backedges, sourcePath);

        return new WorkflowPhaseSkeleton(workflowId, phases, routes, backedges);
    }

    private static WorkflowPhaseDefinition ReadPhase(JsonElement element, string sourcePath, string location)
    {
        EnsureObjectProperties(
            element,
            sourcePath,
            location,
            "id",
            "prefix",
            "mode",
            "entries",
            "exits",
            "imports",
            "exports",
            "dependencies",
            "gateIds",
            "terminalRoutes");

        var id = ReadRequiredString(element, "id", sourcePath, location);
        var mode = ReadRequiredString(element, "mode", sourcePath, location);
        if (mode is not ("phaseSliced" or "legacy"))
        {
            throw Invalid(sourcePath, $"{location}.mode must be 'phaseSliced' or 'legacy'.");
        }

        var prefix = ReadRequiredString(element, "prefix", sourcePath, location, allowEmpty: mode == "legacy");
        var entries = ReadStringArray(element, "entries", sourcePath, location, requireItems: true);
        var exits = ReadStringArray(element, "exits", sourcePath, location, requireItems: true);
        var imports = ReadStringArray(element, "imports", sourcePath, location);
        var exports = ReadStringArray(element, "exports", sourcePath, location);
        var dependencies = ReadStringArray(element, "dependencies", sourcePath, location);
        var gateIds = ReadStringArray(element, "gateIds", sourcePath, location);
        var terminalRoutes = ReadStringArray(element, "terminalRoutes", sourcePath, location);

        if (mode == "phaseSliced")
        {
            var expectedPrefix = id + ".";
            if (!string.Equals(prefix, expectedPrefix, StringComparison.Ordinal))
            {
                throw Invalid(sourcePath, $"{location}.prefix must equal its stable phase id plus '.'.");
            }

            if (entries.Concat(exits).Any(nodeId => !nodeId.StartsWith(prefix, StringComparison.Ordinal)))
            {
                throw Invalid(sourcePath, $"{location} entry and exit ids must use the '{prefix}' phase prefix.");
            }
        }

        return new WorkflowPhaseDefinition(id, prefix, mode, entries, exits, imports, exports, dependencies, gateIds, terminalRoutes);
    }

    private static IReadOnlyList<WorkflowPhaseEdge> ReadEdges(JsonElement root, string propertyName, string sourcePath)
    {
        if (!root.TryGetProperty(propertyName, out var edgesElement) || edgesElement.ValueKind != JsonValueKind.Array)
        {
            throw Invalid(sourcePath, $"{propertyName} must be an array.");
        }

        var edges = new List<WorkflowPhaseEdge>();
        var seen = new HashSet<WorkflowPhaseEdge>();
        var index = 0;
        foreach (var element in edgesElement.EnumerateArray())
        {
            var location = $"{propertyName}[{index}]";
            EnsureObjectProperties(element, sourcePath, location, "fromPhaseId", "fromExitId", "toPhaseId", "toEntryId");
            var edge = new WorkflowPhaseEdge(
                ReadRequiredString(element, "fromPhaseId", sourcePath, location),
                ReadRequiredString(element, "fromExitId", sourcePath, location),
                ReadRequiredString(element, "toPhaseId", sourcePath, location),
                ReadRequiredString(element, "toEntryId", sourcePath, location));
            if (!seen.Add(edge))
            {
                throw Invalid(sourcePath, $"{location} duplicates a declared phase edge.");
            }

            edges.Add(edge);
            index++;
        }

        return edges;
    }

    private static void ValidatePhaseReferences(
        IReadOnlyList<WorkflowPhaseDefinition> phases,
        IReadOnlyDictionary<string, WorkflowPhaseDefinition> phaseById,
        IReadOnlyList<WorkflowPhaseEdge> routes,
        IReadOnlyList<WorkflowPhaseEdge> backedges,
        string sourcePath)
    {
        var nodeOwners = new Dictionary<string, string>(StringComparer.Ordinal);
        var prefixes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var phase in phases)
        {
            if (phase.Prefix.Length > 0 && !prefixes.Add(phase.Prefix))
            {
                throw Invalid(sourcePath, $"Phase prefix '{phase.Prefix}' is duplicated.");
            }

            foreach (var nodeId in phase.Entries.Concat(phase.Exits).Distinct(StringComparer.Ordinal))
            {
                if (nodeOwners.TryGetValue(nodeId, out var ownerPhaseId)
                    && !string.Equals(ownerPhaseId, phase.Id, StringComparison.Ordinal))
                {
                    throw Invalid(sourcePath, $"Node id '{nodeId}' is listed by both phase '{ownerPhaseId}' and phase '{phase.Id}'.");
                }

                nodeOwners[nodeId] = phase.Id;
            }

            foreach (var dependency in phase.Dependencies)
            {
                if (!phaseById.ContainsKey(dependency))
                {
                    throw Invalid(sourcePath, $"Phase '{phase.Id}' depends on unknown phase '{dependency}'.");
                }

                if (string.Equals(dependency, phase.Id, StringComparison.Ordinal))
                {
                    throw Invalid(sourcePath, $"Phase '{phase.Id}' cannot depend on itself.");
                }
            }
        }

        ValidateDependencyAcyclicity(phases, phaseById, sourcePath);
        var seenEdges = new HashSet<WorkflowPhaseEdge>();
        foreach (var edge in routes.Concat(backedges))
        {
            if (!seenEdges.Add(edge))
            {
                throw Invalid(sourcePath, $"Phase edge from '{edge.FromPhaseId}' to '{edge.ToPhaseId}' is declared more than once.");
            }

            if (!phaseById.TryGetValue(edge.FromPhaseId, out var fromPhase)
                || !phaseById.TryGetValue(edge.ToPhaseId, out var toPhase))
            {
                throw Invalid(sourcePath, $"Phase edge references an unknown phase ('{edge.FromPhaseId}' to '{edge.ToPhaseId}').");
            }

            if (string.Equals(fromPhase.Id, toPhase.Id, StringComparison.Ordinal))
            {
                throw Invalid(sourcePath, $"Phase edge '{fromPhase.Id}' to itself must remain phase-local, not appear in routes or backedges.");
            }

            if (!fromPhase.Exits.Contains(edge.FromExitId, StringComparer.Ordinal)
                || !toPhase.Entries.Contains(edge.ToEntryId, StringComparer.Ordinal))
            {
                throw Invalid(sourcePath, $"Phase edge '{edge.FromPhaseId}.{edge.FromExitId}' to '{edge.ToPhaseId}.{edge.ToEntryId}' must reference a declared exit and entry.");
            }
        }
        foreach (var edgeGroup in routes.Concat(backedges)
            .GroupBy(static edge => (edge.FromPhaseId, edge.FromExitId)))
        {
            if (edgeGroup.Select(static edge => (edge.ToPhaseId, edge.ToEntryId))
                .Distinct()
                .Skip(1)
                .Any())
            {
                throw Invalid(
                    sourcePath,
                    $"Phase exit '{edgeGroup.Key.FromPhaseId}.{edgeGroup.Key.FromExitId}' has more than one cross-phase target.");
            }
        }
    }

    private static void ValidateDependencyAcyclicity(
        IReadOnlyList<WorkflowPhaseDefinition> phases,
        IReadOnlyDictionary<string, WorkflowPhaseDefinition> phaseById,
        string sourcePath)
    {
        var marks = new Dictionary<string, byte>(StringComparer.Ordinal);
        bool Visit(string phaseId)
        {
            if (marks.TryGetValue(phaseId, out var mark))
            {
                return mark == 2;
            }

            marks[phaseId] = 1;
            foreach (var dependency in phaseById[phaseId].Dependencies)
            {
                if (marks.TryGetValue(dependency, out var dependencyMark) && dependencyMark == 1)
                {
                    return false;
                }

                if (!Visit(dependency))
                {
                    return false;
                }
            }

            marks[phaseId] = 2;
            return true;
        }

        foreach (var phase in phases)
        {
            if (!Visit(phase.Id))
            {
                throw Invalid(sourcePath, $"Phase dependencies contain a cycle involving '{phase.Id}'. Declare control-flow backedges separately.");
            }
        }
    }

    private static IReadOnlyList<string> ReadStringArray(
        JsonElement element,
        string propertyName,
        string sourcePath,
        string location,
        bool requireItems = false)
    {
        if (!element.TryGetProperty(propertyName, out var values) || values.ValueKind != JsonValueKind.Array)
        {
            throw Invalid(sourcePath, $"{location}.{propertyName} must be an array.");
        }

        var items = new List<string>();
        var unique = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values.EnumerateArray())
        {
            if (value.ValueKind != JsonValueKind.String)
            {
                throw Invalid(sourcePath, $"{location}.{propertyName} entries must be strings.");
            }

            var text = value.GetString();
            if (string.IsNullOrWhiteSpace(text) || !string.Equals(text, text.Trim(), StringComparison.Ordinal))
            {
                throw Invalid(sourcePath, $"{location}.{propertyName} entries must be non-empty trimmed strings.");
            }

            if (!unique.Add(text))
            {
                throw Invalid(sourcePath, $"{location}.{propertyName} contains duplicate value '{text}'.");
            }

            items.Add(text);
        }

        if (requireItems && items.Count == 0)
        {
            throw Invalid(sourcePath, $"{location}.{propertyName} must not be empty.");
        }

        return items;
    }

    private static string ReadRequiredString(
        JsonElement element,
        string propertyName,
        string sourcePath,
        string location,
        bool allowEmpty = false)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String)
        {
            throw Invalid(sourcePath, $"{location}.{propertyName} must be a string.");
        }

        var text = value.GetString();
        if (text is null
            || (!allowEmpty && string.IsNullOrWhiteSpace(text))
            || !string.Equals(text, text.Trim(), StringComparison.Ordinal))
        {
            throw Invalid(sourcePath, $"{location}.{propertyName} must be a non-empty trimmed string.");
        }

        return text;
    }

    private static void EnsureObjectProperties(JsonElement element, string sourcePath, string location, params string[] allowedProperties)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw Invalid(sourcePath, $"{location} must be an object.");
        }

        var allowed = allowedProperties.ToHashSet(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (!seen.Add(property.Name))
            {
                throw Invalid(sourcePath, $"{location} contains duplicate property '{property.Name}'.");
            }

            if (!allowed.Contains(property.Name))
            {
                throw Invalid(sourcePath, $"{location} contains unsupported property '{property.Name}'.");
            }
        }
    }

    private static InvalidOperationException Invalid(string sourcePath, string message)
        => new($"Workflow skeleton '{sourcePath}': {message}");
}

public enum WorkflowPhaseDiagnosticSeverity
{
    Warning,
    Error,
}

public sealed record WorkflowPhaseDiagnostic(
    WorkflowPhaseDiagnosticSeverity Severity,
    string Code,
    string Message,
    string? PhaseId = null,
    string? ContextPath = null);

public sealed record WorkflowPhasePortDescriptor(
    string PhaseId,
    string Direction,
    string Path,
    string Type,
    bool Nullable,
    string Owner,
    string? ItemType,
    string? ValueType);

public sealed class WorkflowPhaseSkeletonValidationResult
{
    internal WorkflowPhaseSkeletonValidationResult(
        IReadOnlyList<WorkflowPhaseDiagnostic> diagnostics,
        IReadOnlyList<WorkflowPhasePortDescriptor> ports)
    {
        Diagnostics = diagnostics;
        Ports = ports;
    }

    public IReadOnlyList<WorkflowPhaseDiagnostic> Diagnostics { get; }

    public IReadOnlyList<WorkflowPhasePortDescriptor> Ports { get; }

    public bool IsValid => Diagnostics.All(static diagnostic => diagnostic.Severity != WorkflowPhaseDiagnosticSeverity.Error);
}

public static class WorkflowPhaseSkeletonValidator
{
    public static WorkflowPhaseSkeletonValidationResult Validate(
        WorkflowPhaseSkeleton skeleton,
        ContextFieldCatalog catalog,
        string sourcePath)
    {
        ArgumentNullException.ThrowIfNull(skeleton);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        var diagnostics = new List<WorkflowPhaseDiagnostic>();
        var ports = new List<WorkflowPhasePortDescriptor>();
        var exportsByPath = new Dictionary<string, string>(StringComparer.Ordinal);
        var exportedPaths = new List<(string Path, string PhaseId)>();
        var producerConflictPairs = new HashSet<(string FirstPath, string SecondPath)>();
        var callerPrefixErrors = new HashSet<string>(StringComparer.Ordinal);

        foreach (var phase in skeleton.Phases)
        {
            if (phase.Mode == "legacy")
            {
                diagnostics.Add(new WorkflowPhaseDiagnostic(
                    WorkflowPhaseDiagnosticSeverity.Warning,
                    "LOOM.PHASE.LEGACY_PHASE",
                    $"Phase '{phase.Id}' uses legacy node naming; its declared ports remain subject to catalog and dependency checks.",
                    phase.Id));
            }

            foreach (var path in phase.Exports)
            {
                if (!catalog.TryGetField(path, out var field))
                {
                    AddUnknownPort(diagnostics, sourcePath, phase.Id, "export", path);
                    continue;
                }

                ports.Add(ToPort(phase.Id, "export", field));
                ValidateCallerPrefix(diagnostics, callerPrefixErrors, phase.Id, path, field.Owner);
                if (!exportsByPath.TryAdd(path, phase.Id))
                {
                    diagnostics.Add(new WorkflowPhaseDiagnostic(
                        WorkflowPhaseDiagnosticSeverity.Error,
                        "LOOM.PHASE.MULTIPLE_PRODUCERS",
                        $"Context path '{path}' is exported by more than one phase; each shared business field must have exactly one producing phase.",
                        phase.Id,
                        path));
                }

                foreach (var existing in exportedPaths)
                {
                    if (string.Equals(existing.PhaseId, phase.Id, StringComparison.Ordinal)
                        || string.Equals(existing.Path, path, StringComparison.Ordinal)
                        || !PathsOverlap(existing.Path, path))
                    {
                        continue;
                    }

                    var firstPath = StringComparer.Ordinal.Compare(existing.Path, path) < 0 ? existing.Path : path;
                    var secondPath = string.Equals(firstPath, existing.Path, StringComparison.Ordinal) ? path : existing.Path;
                    if (producerConflictPairs.Add((firstPath, secondPath)))
                    {
                        diagnostics.Add(new WorkflowPhaseDiagnostic(
                            WorkflowPhaseDiagnosticSeverity.Error,
                            "LOOM.PHASE.PARENT_CHILD_PRODUCER_CONFLICT",
                            $"Overlapping context paths '{existing.Path}' and '{path}' are exported by different phases; assign the parent and child fields to one producing phase.",
                            phase.Id,
                            path));
                    }
                }

                exportedPaths.Add((path, phase.Id));
            }
        }

        foreach (var phase in skeleton.Phases)
        {
            foreach (var path in phase.Imports)
            {
                if (!catalog.TryGetField(path, out var field))
                {
                    AddUnknownPort(diagnostics, sourcePath, phase.Id, "import", path);
                    continue;
                }

                ports.Add(ToPort(phase.Id, "import", field));
                ValidateCallerPrefix(diagnostics, callerPrefixErrors, phase.Id, path, field.Owner);
                var producerPhaseIds = exportedPaths
                    .Where(export => PathsOverlap(export.Path, path))
                    .Select(static export => export.PhaseId)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                if (producerPhaseIds.Length == 0)
                {
                    if (string.Equals(field.Owner, "agent", StringComparison.Ordinal))
                    {
                        diagnostics.Add(new WorkflowPhaseDiagnostic(
                            WorkflowPhaseDiagnosticSeverity.Error,
                            "LOOM.PHASE.FIELD_PRODUCER_MISSING",
                            $"Agent-owned context path '{path}' is imported without a declared producing phase.",
                            phase.Id,
                            path));
                    }

                    continue;
                }

                foreach (var producerPhaseId in producerPhaseIds)
                {
                    if (!string.Equals(producerPhaseId, phase.Id, StringComparison.Ordinal)
                        && !phase.Dependencies.Contains(producerPhaseId, StringComparer.Ordinal))
                    {
                        diagnostics.Add(new WorkflowPhaseDiagnostic(
                            WorkflowPhaseDiagnosticSeverity.Error,
                            "LOOM.PHASE.IMPORT_DEPENDENCY_MISSING",
                            $"Phase '{phase.Id}' imports '{path}' from producer phase '{producerPhaseId}' without declaring that dependency.",
                            phase.Id,
                            path));
                    }
                }
            }
        }

        return new WorkflowPhaseSkeletonValidationResult(diagnostics, ports);
    }

    private static void AddUnknownPort(
        ICollection<WorkflowPhaseDiagnostic> diagnostics,
        string sourcePath,
        string phaseId,
        string direction,
        string path)
    {
        diagnostics.Add(new WorkflowPhaseDiagnostic(
            WorkflowPhaseDiagnosticSeverity.Error,
            "LOOM.PHASE.PORT_UNKNOWN",
            $"Workflow skeleton '{sourcePath}' phase '{phaseId}' declares {direction} '{path}', which is not present in the owning context_fields catalog.",
            phaseId,
            path));
    }

    private static void ValidateCallerPrefix(
        ICollection<WorkflowPhaseDiagnostic> diagnostics,
        ISet<string> reportedPaths,
        string phaseId,
        string path,
        string owner)
    {
        if (string.Equals(owner, "caller", StringComparison.Ordinal)
            && !path.StartsWith("p00_input.", StringComparison.Ordinal)
            && reportedPaths.Add(path))
        {
            diagnostics.Add(new WorkflowPhaseDiagnostic(
                WorkflowPhaseDiagnosticSeverity.Error,
                "LOOM.PHASE.CALLER_INPUT_PREFIX",
                $"Caller-owned context path '{path}' must use the 'p00_input.' prefix.",
                phaseId,
                path));
        }
    }

    private static bool PathsOverlap(string first, string second)
        => string.Equals(first, second, StringComparison.Ordinal)
            || first.StartsWith(second + ".", StringComparison.Ordinal)
            || second.StartsWith(first + ".", StringComparison.Ordinal);

    private static WorkflowPhasePortDescriptor ToPort(string phaseId, string direction, ContextFieldDefinition field)
        => new(phaseId, direction, field.Path, field.Type, field.Nullable, field.Owner, field.ItemType, field.ValueType);
}
