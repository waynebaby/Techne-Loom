using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class UserInputContractValidationTests
{
    [Fact]
    public void UserInputContract_AcceptsValidAskUserAndSerializedRequiredInputs()
    {
        var transition = CreateValidTransition();
        var instance = new WorkflowInstance
        {
            InstanceId = "ask-user-valid",
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                ["state.done"] = new StateNode { Id = "state.done" },
                [transition.Id] = transition,
            },
        };
        var roundTrip = WorkflowJsonSerializer.Deserialize(WorkflowJsonSerializer.Serialize(instance));

        Assert.Empty(UserInputContractValidator.Validate(roundTrip));
    }

    [Fact]
    public void UserInputContract_RejectsUnsupportedVersionAndNonAskUserStep()
    {
        var wrongStepTransition = new CommandTransition
        {
            Id = "transition.wrong-step",
            StepKind = WorkflowStepKind.Plan,
            UserInput = CreateValidTransition().UserInput,
        };

        var wrongStepDiagnostic = Assert.Single(UserInputContractValidator.Validate([wrongStepTransition]));
        Assert.EndsWith("/userInput", wrongStepDiagnostic.Location, StringComparison.Ordinal);

        var versionTransition = CreateValidTransition();
        versionTransition.UserInput!.Version = 2;

        var versionDiagnostic = Assert.Single(UserInputContractValidator.Validate([versionTransition]));
        Assert.EndsWith("/userInput/version", versionDiagnostic.Location, StringComparison.Ordinal);
    }

    [Fact]
    public void UserInputContract_RejectsDuplicateIdsAndUnboundContextPaths()
    {
        var transition = CreateValidTransition();
        var firstGroup = transition.UserInput!.QuestionGroups[0];
        var duplicateQuestion = new UserInputQuestion
        {
            Id = "question.display-name",
            Context = "Choose a different display name.",
            Intent = "Personalize the report.",
            Prompt = "What name should appear?",
            ContextPath = "answers.unlisted",
            Type = UserInputQuestionTypes.Text,
            Required = true,
        };
        transition.UserInput.QuestionGroups.Add(new UserInputQuestionGroup
        {
            Id = firstGroup.Id,
            Title = "Duplicate group",
            Questions = [duplicateQuestion],
        });

        var diagnostics = UserInputContractValidator.Validate([transition]);

        Assert.Contains(diagnostics, item => item.Location.EndsWith("/id", StringComparison.Ordinal) && item.Message.Contains("Question group id", StringComparison.Ordinal));
        Assert.Contains(diagnostics, item => item.Location.EndsWith("/id", StringComparison.Ordinal) && item.Message.Contains("Question id", StringComparison.Ordinal));
        Assert.Contains(diagnostics, item => item.Location.EndsWith("/contextPath", StringComparison.Ordinal) && item.Message.Contains("not declared in requiredInputs", StringComparison.Ordinal));
    }

    [Fact]
    public void UserInputContract_RejectsInvalidChoiceDefaultAndMultipleFlag()
    {
        var transition = CreateValidTransition();
        var question = transition.UserInput!.QuestionGroups[0].Questions[0];
        question.Type = UserInputQuestionTypes.MultipleChoice;
        question.Multiple = false;
        question.Options = [new UserInputOption { Value = "red", Label = "Red" }];
        question.DefaultValue = JsonSerializer.SerializeToElement(new[] { "missing" });
        question.Constraints = new UserInputQuestionConstraints { MinSelections = 2, MaxSelections = 1 };

        var diagnostics = UserInputContractValidator.Validate([transition]);

        Assert.Contains(diagnostics, item => item.Location.EndsWith("/multiple", StringComparison.Ordinal));
        Assert.Contains(diagnostics, item => item.Location.EndsWith("/constraints", StringComparison.Ordinal));
        Assert.Contains(diagnostics, item => item.Location.EndsWith("/defaultValue", StringComparison.Ordinal));
    }

    [Fact]
    public void UserInputContract_RejectsSelectionLimitAboveOptionCount()
    {
        var transition = CreateValidTransition();
        var question = transition.UserInput!.QuestionGroups[0].Questions[0];
        question.Type = UserInputQuestionTypes.MultipleChoice;
        question.Options = [new UserInputOption { Value = "red", Label = "Red" }];
        question.Constraints = new UserInputQuestionConstraints { MaxSelections = 2 };

        var diagnostic = Assert.Single(UserInputContractValidator.Validate([transition]));

        Assert.Contains("number of distinct options", diagnostic.Message, StringComparison.Ordinal);
    }

    private static CommandTransition CreateValidTransition()
        => new()
        {
            Id = "transition.ask-user",
            StepKind = WorkflowStepKind.AskUser,
            TargetNodeId = "state.done",
            Command = new CommandInvocation
            {
                Name = "ask_user",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["requiredInputs"] = new object?[] { "answers.displayName" },
                },
            },
            UserInput = new UserInputContract
            {
                QuestionGroups =
                [
                    new UserInputQuestionGroup
                    {
                        Id = "group.identity",
                        Title = "About you",
                        Questions =
                        [
                            new UserInputQuestion
                            {
                                Id = "question.display-name",
                                Context = "Choose a name shown in the report.",
                                Intent = "Personalize the report.",
                                Prompt = "What display name should we use?",
                                ContextPath = "answers.displayName",
                                Type = UserInputQuestionTypes.Text,
                                Required = true,
                                Constraints = new UserInputQuestionConstraints
                                {
                                    MinLength = 1,
                                    MaxLength = 80,
                                },
                            },
                        ],
                    },
                ],
            },
        };
}
