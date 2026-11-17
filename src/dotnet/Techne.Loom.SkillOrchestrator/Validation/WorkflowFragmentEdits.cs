using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp;
using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.SkillOrchestrator.Validation;

internal abstract record WorkflowFragmentEdit
{
    internal abstract WorkflowFragmentId Fragment { get; }

    internal virtual bool ChangesDataflow => true;

    // Begin with a private copy. The mutation defines the sole permitted
    // candidate change, while the returned statement is checked by Roslyn.
    internal abstract string? Stage(WorkflowInstance expected);

    protected static string Literal(string? value) => value is null ? "null" : SymbolDisplay.FormatLiteral(value, quote: true);

    protected static T RequireNode<T>(WorkflowInstance workflow, string id) where T : class, ITaskNode
        => workflow.Nodes.TryGetValue(id, out var node) && node is T target
            ? target
            : throw new InvalidOperationException($"The requested {typeof(T).Name} '{id}' does not exist.");
}

internal sealed record WorkflowStateDescriptionEdit(string StateId, string? Description) : WorkflowFragmentEdit
{
    internal override WorkflowFragmentId Fragment => new(WorkflowFragmentKind.State, StateId);

    internal override bool ChangesDataflow => false;

    internal override string? Stage(WorkflowInstance expected)
    {
        var state = RequireNode<StateNode>(expected, StateId);
        if (string.Equals(state.Description, Description, StringComparison.Ordinal))
        {
            return null;
        }

        state.Description = Description;
        return $"((StateNode)workflow.Nodes[{Literal(StateId)}]).Description = {Literal(Description)};";
    }
}

internal sealed record WorkflowTransitionTargetEdit(string TransitionId, string TargetStateId) : WorkflowFragmentEdit
{
    internal override WorkflowFragmentId Fragment => new(WorkflowFragmentKind.Transition, TransitionId);

    internal override string? Stage(WorkflowInstance expected)
    {
        var transition = RequireNode<CommandTransition>(expected, TransitionId);
        RequireNode<StateNode>(expected, TargetStateId);
        if (string.Equals(transition.TargetNodeId, TargetStateId, StringComparison.Ordinal))
        {
            return null;
        }

        expected.Nodes[TransitionId] = transition with { TargetNodeId = TargetStateId };
        var target = $"workflow.Nodes[{Literal(TransitionId)}]";
        return $"{target} = ((CommandTransition){target}) with {{ TargetNodeId = {Literal(TargetStateId)} }};";
    }
}

internal sealed record WorkflowGroupLinksEdit(string StateId, string GroupId, ImmutableArray<string> TransitionIds) : WorkflowFragmentEdit
{
    internal override WorkflowFragmentId Fragment => new(WorkflowFragmentKind.Group, StateId, GroupId);

    internal override string? Stage(WorkflowInstance expected)
    {
        var state = RequireNode<StateNode>(expected, StateId);
        var groupIndex = state.Groups.FindIndex(group => string.Equals(group.Id, GroupId, StringComparison.Ordinal));
        if (groupIndex < 0 || state.Groups.Count(group => string.Equals(group.Id, GroupId, StringComparison.Ordinal)) != 1)
        {
            throw new InvalidOperationException($"Group '{GroupId}' has no unique match under state '{StateId}'.");
        }

        if (TransitionIds.IsDefault || TransitionIds.Distinct(StringComparer.Ordinal).Count() != TransitionIds.Length)
        {
            throw new InvalidOperationException("Group membership requires a defined list of unique transition IDs.");
        }

        foreach (var id in TransitionIds)
        {
            RequireNode<TransitionBase>(expected, id);
        }

        var group = state.Groups[groupIndex];
        if (group.TransitionIds.SequenceEqual(TransitionIds, StringComparer.Ordinal))
        {
            return null;
        }

        group.TransitionIds = TransitionIds.ToList();
        var ids = string.Join(", ", TransitionIds.Select(Literal));
        var index = groupIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return $"((StateNode)workflow.Nodes[{Literal(StateId)}]).Groups[{index}].TransitionIds = new System.Collections.Generic.List<string> {{ {ids} }};";
    }
}

internal sealed record WorkflowGatePredicateEdit(string GateId, string Source) : WorkflowFragmentEdit
{
    internal override WorkflowFragmentId Fragment => new(WorkflowFragmentKind.Gate, GateId);

    internal override string? Stage(WorkflowInstance expected)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Source);
        if (expected.Validation is not { } validation
            || !validation.Gates.TryGetValue(GateId, out var gate)
            || gate.PassExpression is not { } predicate)
        {
            throw new InvalidOperationException($"Gate '{GateId}' does not contain a predicate to edit.");
        }

        if (string.Equals(predicate.Source, Source, StringComparison.Ordinal))
        {
            return null;
        }

        predicate.Source = Source;
        return $"workflow.Validation.Gates[{Literal(GateId)}].PassExpression.Source = {Literal(Source)};";
    }
}
