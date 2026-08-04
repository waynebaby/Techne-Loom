using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public static class WorkflowInstanceCloner
{
    public static WorkflowInstance Clone(WorkflowInstance source)
    {
        var clone = new WorkflowInstance
        {
            InstanceId = source.InstanceId,
            StartNodeId = source.StartNodeId,
            CurrentNodeId = source.CurrentNodeId,
            EndNodeId = source.EndNodeId,
            TemplateKind = source.TemplateKind,
            Validation = CloneValidation(source.Validation),
            Status = source.Status,
            Context = source.Context.ToDictionary(static pair => pair.Key, static pair => CloneValue(pair.Value), StringComparer.Ordinal),
            History = source.History.Select(Clone).ToList(),
            Version = source.Version,
            ActiveWaitGroups = source.ActiveWaitGroups.Select(Clone).ToList(),
            LastActivityUtc = source.LastActivityUtc,
            LastHeartbeatUtc = source.LastHeartbeatUtc,
            LeaseOwner = source.LeaseOwner,
            LeaseExpiresUtc = source.LeaseExpiresUtc,
        };

        if (source.Nodes is not null)
        {
            clone.Nodes = source.Nodes.ToDictionary(static pair => pair.Key, static pair => CloneNode(pair.Value), StringComparer.Ordinal);
        }

        return clone;
    }

    private static WorkflowHistoryEntry Clone(WorkflowHistoryEntry source)
    {
        return new WorkflowHistoryEntry(
            source.Timestamp,
            source.NodeId,
            source.NodeType,
            source.Status,
            source.ContextChanges is null ? null : source.ContextChanges.ToDictionary(static pair => pair.Key, static pair => CloneValue(pair.Value), StringComparer.Ordinal),
            source.Message);
    }

    private static PendingWaitGroup Clone(PendingWaitGroup source)
    {
        var clone = new PendingWaitGroup
        {
            InstanceId = source.InstanceId,
            TransitionId = source.TransitionId,
            CorrelationKey = source.CorrelationKey,
            TargetStateId = source.TargetStateId,
            TimeoutTargetStateId = source.TimeoutTargetStateId,
            CreatedAt = source.CreatedAt,
            OriginStrategy = source.OriginStrategy,
            Completed = source.Completed,
            CompletedAt = source.CompletedAt,
            CompletionLogged = source.CompletionLogged,
            TimedOut = source.TimedOut,
        };

        foreach (var pair in source.AggregatedContext)
        {
            clone.AggregatedContext[pair.Key] = CloneValue(pair.Value);
        }

        clone.Entries.AddRange(source.Entries.Select(entry => new PendingWaitEntry
        {
            WaitId = entry.WaitId,
            ExpireAt = entry.ExpireAt,
            Completed = entry.Completed,
            CompletedAt = entry.CompletedAt,
            ResultContext = entry.ResultContext is null ? null : entry.ResultContext.ToDictionary(static pair => pair.Key, static pair => CloneValue(pair.Value), StringComparer.Ordinal),
            Error = entry.Error,
        }));

        return clone;
    }

    private static WorkflowValidationContract? CloneValidation(WorkflowValidationContract? source)
    {
        if (source is null)
        {
            return null;
        }

        return new WorkflowValidationContract
        {
            DeclaredUserOwnedFields = new List<string>(source.DeclaredUserOwnedFields),
            ReservedRuntimeOwnedFields = new List<string>(source.ReservedRuntimeOwnedFields),
            Gates = source.Gates.ToDictionary(
                static pair => pair.Key,
                static pair => new WorkflowValidationGate
                {
                    Description = pair.Value.Description,
                    PassExpression = pair.Value.PassExpression,
                    RequiredOutputFamilies = new List<string>(pair.Value.RequiredOutputFamilies),
                    RequiredMachineReadableOutputFamilies = new List<string>(pair.Value.RequiredMachineReadableOutputFamilies),
                    RequiredHumanReviewableOutputFamilies = new List<string>(pair.Value.RequiredHumanReviewableOutputFamilies),
                },
                StringComparer.Ordinal),
            Routes = source.Routes.ToDictionary(
                static pair => pair.Key,
                static pair => new WorkflowRouteValidationProfile
                {
                    Description = pair.Value.Description,
                    RequiredTerminalGateIds = new List<string>(pair.Value.RequiredTerminalGateIds),
                    RequiredBlockedGateIds = new List<string>(pair.Value.RequiredBlockedGateIds),
                },
                StringComparer.Ordinal),
        };
    }

    private static ITaskNode CloneNode(ITaskNode node)
    {
        return node switch
        {
            StateNode stateNode => new StateNode
            {
                Id = stateNode.Id,
                Name = stateNode.Name,
                Description = stateNode.Description,
                WorkflowPhase = stateNode.WorkflowPhase,
                Groups = stateNode.Groups.Select(group => new TransitionGroup
                {
                    Id = group.Id,
                    Strategy = group.Strategy,
                    GroupTimeout = group.GroupTimeout,
                    CancelLosers = group.CancelLosers,
                    TimeoutTransition = group.TimeoutTransition is null ? null : (TransitionBase)CloneNode(group.TimeoutTransition),
                    TimeoutTargetStateId = group.TimeoutTargetStateId,
                    TransitionIds = new List<string>(group.TransitionIds),
                }).ToList(),
                Expiration = stateNode.Expiration,
                EntranceTime = stateNode.EntranceTime,
                WaitBehavior = stateNode.WaitBehavior,
                CorrelationKeyPath = stateNode.CorrelationKeyPath,
                StateFailedExpression = stateNode.StateFailedExpression,
            },
            CommandTransition commandTransition => commandTransition with
            {
                Command = (CommandInvocation)commandTransition.Command.Clone(),
                GuardExpressionWasExplicitlyDeclared = commandTransition.GuardExpressionWasExplicitlyDeclared,
                SucceedExpressionWasExplicitlyDeclared = commandTransition.SucceedExpressionWasExplicitlyDeclared,
            },
            ExpressionTransition expressionTransition => expressionTransition with { GuardExpressionWasExplicitlyDeclared = expressionTransition.GuardExpressionWasExplicitlyDeclared, SucceedExpressionWasExplicitlyDeclared = expressionTransition.SucceedExpressionWasExplicitlyDeclared },
            ToBeRefinedTransition refineTransition => refineTransition with { GuardExpressionWasExplicitlyDeclared = refineTransition.GuardExpressionWasExplicitlyDeclared, SucceedExpressionWasExplicitlyDeclared = refineTransition.SucceedExpressionWasExplicitlyDeclared },
            TransitionBase transitionBase => transitionBase with { },
            _ => throw new NotSupportedException($"Unsupported task node type '{node.GetType().FullName}'."),
        };
    }

    private static object? CloneValue(object? value)
    {
        return value switch
        {
            null => null,
            Dictionary<string, object?> dictionary => dictionary.ToDictionary(static pair => pair.Key, static pair => CloneValue(pair.Value), StringComparer.Ordinal),
            IDictionary<string, object?> dictionary => dictionary.ToDictionary(static pair => pair.Key, static pair => CloneValue(pair.Value), StringComparer.Ordinal),
            IReadOnlyDictionary<string, object?> dictionary => dictionary.ToDictionary(static pair => pair.Key, static pair => CloneValue(pair.Value), StringComparer.Ordinal),
            List<object?> list => list.Select(CloneValue).ToList(),
            IReadOnlyList<object?> list => list.Select(CloneValue).ToList(),
            _ => value,
        };
    }
}
