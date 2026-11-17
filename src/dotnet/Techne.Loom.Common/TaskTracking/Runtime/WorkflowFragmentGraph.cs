using System.Globalization;
using System.Text.Json.Serialization;
using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.Common.TaskTracking.Runtime;

internal enum WorkflowFragmentKind { State, Transition, Group, Gate, Route }

[method: JsonConstructor]
internal readonly record struct WorkflowFragmentId(WorkflowFragmentKind Kind, string Owner, string? Slot = null);

internal sealed class WorkflowFragmentGraph
{
    private readonly Dictionary<WorkflowFragmentId, string> _locations = [];
    private readonly Dictionary<WorkflowFragmentId, HashSet<WorkflowFragmentId>> _consumers = [];
    private bool _hasUnresolvedReferences;

    internal IReadOnlyDictionary<WorkflowFragmentId, string> Locations => _locations;

    internal static WorkflowFragmentGraph Create(WorkflowInstance draft)
    {
        var graph = new WorkflowFragmentGraph();
        foreach (var (key, node) in draft.Nodes)
        {
            var id = GetNodeId(key, node);
            graph.Add(id, $"/nodes/{Escape(key)}");
            if (node is not StateNode state)
            {
                continue;
            }

            for (var index = 0; index < state.Groups.Count; index++)
            {
                var group = state.Groups[index];
                graph.Add(new(WorkflowFragmentKind.Group, key, group.Id),
                    $"/nodes/{Escape(key)}/groups/{index.ToString(CultureInfo.InvariantCulture)}");
            }
        }

        if (draft.Validation is { } validation)
        {
            foreach (var key in validation.Gates.Keys)
            {
                graph.Add(new(WorkflowFragmentKind.Gate, key), $"/validation/gates/{Escape(key)}");
            }

            foreach (var (key, route) in validation.Routes)
            {
                var id = new WorkflowFragmentId(WorkflowFragmentKind.Route, key);
                graph.Add(id, $"/validation/routes/{Escape(key)}");
                foreach (var gate in route.RequiredTerminalGateIds.Concat(route.RequiredBlockedGateIds))
                {
                    graph.DependsOn(id, new(WorkflowFragmentKind.Gate, gate));
                }
            }
        }

        foreach (var (key, node) in draft.Nodes)
        {
            var id = GetNodeId(key, node);
            if (node is TransitionBase transition)
            {
                graph.LinkTransition(id, transition);
                continue;
            }

            foreach (var group in ((StateNode)node).Groups)
            {
                var groupId = new WorkflowFragmentId(WorkflowFragmentKind.Group, key, group.Id);
                graph.DependsOn(groupId, id);
                foreach (var transitionId in group.TransitionIds)
                {
                    var member = new WorkflowFragmentId(WorkflowFragmentKind.Transition, transitionId);
                    graph.DependsOn(groupId, member);
                    graph.DependsOn(member, groupId);
                }

                graph.DependsOnTarget(groupId, group.JoinTargetStateId);
                graph.DependsOnTarget(groupId, group.NoPassTargetStateId);
                graph.DependsOnTarget(groupId, group.ErrorHandlingTargetStateId);
                graph.DependsOnTarget(groupId, group.TimeoutTargetStateId);
                if (group.TimeoutTransition is { } timeout)
                {
                    graph.LinkTransition(groupId, timeout);
                }
            }
        }

        return graph;
    }

    // This graph covers declared topology and route references. Expression and
    // context dependencies may be dynamic, so callers must invalidate broadly for those edits.
    internal static IReadOnlySet<WorkflowFragmentId> Impact(
        WorkflowFragmentGraph previous,
        WorkflowFragmentGraph candidate,
        IEnumerable<WorkflowFragmentId> changed,
        bool hasUnindexedDependencies)
    {
        var impacted = changed.ToHashSet();
        if (hasUnindexedDependencies || previous._hasUnresolvedReferences || candidate._hasUnresolvedReferences
            || impacted.Any(id => !previous._locations.ContainsKey(id) && !candidate._locations.ContainsKey(id)))
        {
            impacted.UnionWith(previous._locations.Keys);
            impacted.UnionWith(candidate._locations.Keys);
            return impacted;
        }

        var pending = new Queue<WorkflowFragmentId>(impacted);
        while (pending.TryDequeue(out var dependency))
        {
            foreach (var consumer in previous.GetConsumers(dependency).Concat(candidate.GetConsumers(dependency)))
            {
                if (impacted.Add(consumer))
                {
                    pending.Enqueue(consumer);
                }
            }
        }

        return impacted;
    }

    private void LinkTransition(WorkflowFragmentId id, TransitionBase transition)
    {
        DependsOnTarget(id, transition.TargetNodeId);
        var parameters = (transition as CommandTransition)?.Command.Parameters;
        foreach (var gate in ReadTransitionStrings(transition.SatisfiesGateIds, parameters, "satisfiesGateIds"))
        {
            var gateId = new WorkflowFragmentId(WorkflowFragmentKind.Gate, gate);
            DependsOn(gateId, id);
            DependsOn(id, gateId);
        }

        var terminal = ReadTransitionStrings(transition.TerminalRoutes, parameters, "terminalRoutes");
        var blocked = ReadTransitionStrings(transition.BlockedRoutes, parameters, "blockedRoutes");
        foreach (var route in terminal.Concat(blocked))
        {
            var routeId = new WorkflowFragmentId(WorkflowFragmentKind.Route, route);
            DependsOn(routeId, id);
            DependsOn(id, routeId);
        }
    }

    internal static List<string> ReadTransitionStrings(
        IReadOnlyList<string>? declared, IReadOnlyDictionary<string, object?>? parameters, string key)
    {
        if (declared is { Count: > 0 })
        {
            return declared.Where(static item => !string.IsNullOrWhiteSpace(item)).ToList();
        }

        return ReadStringList(parameters, key);
    }

    internal static List<string> ReadStringList(IReadOnlyDictionary<string, object?>? parameters, string key)
    {
        if (parameters is null || !parameters.TryGetValue(key, out var value))
        {
            return [];
        }

        return value switch
        {
            string text when !string.IsNullOrWhiteSpace(text) => [text],
            IEnumerable<string> items => items.Where(static item => !string.IsNullOrWhiteSpace(item)).ToList(),
            IEnumerable<object?> items => items.Select(Convert.ToString).Where(static item => !string.IsNullOrWhiteSpace(item)).Cast<string>().ToList(),
            _ => [],
        };
    }

    private void DependsOnTarget(WorkflowFragmentId consumer, string? stateId)
    {
        if (stateId is not null)
        {
            DependsOn(consumer, new(WorkflowFragmentKind.State, stateId));
        }
    }

    private void DependsOn(WorkflowFragmentId consumer, WorkflowFragmentId dependency)
    {
        if (!_locations.ContainsKey(consumer) || !_locations.ContainsKey(dependency))
        {
            _hasUnresolvedReferences = true;
            return;
        }

        if (!_consumers.TryGetValue(dependency, out var consumers))
        {
            consumers = [];
            _consumers.Add(dependency, consumers);
        }

        consumers.Add(consumer);
    }

    private IEnumerable<WorkflowFragmentId> GetConsumers(WorkflowFragmentId id)
        => _consumers.TryGetValue(id, out var consumers) ? consumers : [];

    private void Add(WorkflowFragmentId id, string pointer)
    {
        if (string.IsNullOrWhiteSpace(id.Owner)
            || (id.Kind == WorkflowFragmentKind.Group && string.IsNullOrWhiteSpace(id.Slot))
            || !_locations.TryAdd(id, pointer))
        {
            throw new InvalidOperationException($"A fragment identity is missing or duplicated at '{pointer}'.");
        }
    }

    private static WorkflowFragmentId GetNodeId(string key, ITaskNode node)
        => node switch
        {
            StateNode => new(WorkflowFragmentKind.State, key),
            TransitionBase => new(WorkflowFragmentKind.Transition, key),
            _ => throw new InvalidOperationException($"Unsupported workflow node at '/nodes/{Escape(key)}'."),
        };

    private static string Escape(string segment)
        => segment.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);
}
