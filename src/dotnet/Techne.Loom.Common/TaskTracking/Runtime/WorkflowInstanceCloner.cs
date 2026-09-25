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
            TaskType = source.TaskType,
            WorkflowKind = source.WorkflowKind,
            CaseId = source.CaseId,
            RunId = source.RunId,
            RuntimeBinding = source.RuntimeBinding,
            RuntimeVersion = source.RuntimeVersion,
            ExpressionBinding = CloneExpressionBinding(source.ExpressionBinding),
            Validation = CloneValidation(source.Validation),
            LastGateEvaluation = CloneGateEvaluation(source.LastGateEvaluation),
            Status = source.Status,
            Context = source.Context.ToDictionary(static pair => pair.Key, static pair => DeepValueCloner.Clone(pair.Value), StringComparer.Ordinal),
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

        if (WorkflowRuntimeEvidenceRegistry.IsObserved(source))
        {
            WorkflowRuntimeEvidenceRegistry.MarkObserved(clone);
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
            source.ContextChanges is null ? null : source.ContextChanges.ToDictionary(static pair => pair.Key, static pair => DeepValueCloner.Clone(pair.Value), StringComparer.Ordinal),
            source.Message);
    }

    private static PendingWaitGroup Clone(PendingWaitGroup source)
    {
        var clone = new PendingWaitGroup
        {
            InstanceId = source.InstanceId,
            TransitionId = source.TransitionId,
            ConcurrencyGroupId = source.ConcurrencyGroupId,
            ExpectedTransitionIds = new List<string>(source.ExpectedTransitionIds),
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
            clone.AggregatedContext[pair.Key] = DeepValueCloner.Clone(pair.Value);
        }

        clone.Entries.AddRange(source.Entries.Select(entry => new PendingWaitEntry
        {
            WaitId = entry.WaitId,
            ExpireAt = entry.ExpireAt,
            Completed = entry.Completed,
            CompletedAt = entry.CompletedAt,
            ResultContext = entry.ResultContext is null ? null : entry.ResultContext.ToDictionary(static pair => pair.Key, static pair => DeepValueCloner.Clone(pair.Value), StringComparer.Ordinal),
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
                    PassExpression = CloneExpression(pair.Value.PassExpression),
                    RequiredOutputFamilies = new List<string>(pair.Value.RequiredOutputFamilies),
                    RequiredMachineReadableOutputFamilies = new List<string>(pair.Value.RequiredMachineReadableOutputFamilies),
                    RequiredHumanReviewableOutputFamilies = new List<string>(pair.Value.RequiredHumanReviewableOutputFamilies),
                    ValueSemantics = new Dictionary<string, string>(pair.Value.ValueSemantics, StringComparer.Ordinal),
                    InstanceBinding = pair.Value.InstanceBinding,
                    FailureGuidance = CloneFailureGuidance(pair.Value.FailureGuidance),
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

    private static GateEvaluationResult? CloneGateEvaluation(GateEvaluationResult? source)
    {
        return source is null
            ? null
            : source with
            {
                ExpectedPayloadShape = source.ExpectedPayloadShape,
                ReceivedPayloadTopLevelKeys = source.ReceivedPayloadTopLevelKeys.ToList(),
                RequiredInputs = source.RequiredInputs.ToList(),
                ResumeOutputKey = source.ResumeOutputKey,
                OutputPath = source.OutputPath,
                ProjectedContextPaths = source.ProjectedContextPaths.ToList(),
                MissingOutputFamilies = source.MissingOutputFamilies.ToList(),
                EmptyOutputFamilies = source.EmptyOutputFamilies.ToList(),
                ResolvedOutputPaths = new Dictionary<string, string?>(source.ResolvedOutputPaths, StringComparer.Ordinal),
            };
    }

    private static WorkflowGateFailureGuidance? CloneFailureGuidance(WorkflowGateFailureGuidance? source)
    {
        return source is null
            ? null
            : new WorkflowGateFailureGuidance
            {
                Summary = source.Summary,
                NextAction = source.NextAction,
                EvidenceReferences = source.EvidenceReferences.Select(reference => new WorkflowEvidenceReference
                {
                    Path = reference.Path,
                    StartLine = reference.StartLine,
                    EndLine = reference.EndLine,
                    Quote = reference.Quote,
                }).ToList(),
            };
    }

    private static ExpressionBinding CloneExpressionBinding(ExpressionBinding source)
    {
        return new ExpressionBinding
        {
            Language = source.Language,
            LanguageVersion = source.LanguageVersion,
            ContractId = source.ContractId,
            ContractVersion = source.ContractVersion,
            RequiredExpressionCapabilities = new List<string>(source.RequiredExpressionCapabilities),
            CompileFeedbackContract = source.CompileFeedbackContract,
        };
    }

    private static ExpressionDefinition? CloneExpression(ExpressionDefinition? source)
    {
        return source is null
            ? null
            : new ExpressionDefinition
            {
                Kind = source.Kind,
                Source = source.Source,
                EntryPoint = source.EntryPoint,
                ResultType = source.ResultType,
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
                StateFailedExpression = CloneExpression(stateNode.StateFailedExpression),
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
}
