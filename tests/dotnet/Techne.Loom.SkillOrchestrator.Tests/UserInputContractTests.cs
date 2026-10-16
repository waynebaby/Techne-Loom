using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class UserInputContractTests
{
    [Fact]
    public void CommandTransition_UserInputIsOptionalAndRoundTrips()
    {
        var typedTransition = new CommandTransition
        {
            Id = "transition.typed-ask",
            StepKind = WorkflowStepKind.AskUser,
            TargetNodeId = "state.done",
            Command = new CommandInvocation
            {
                Name = "ask_user",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["requiredInputs"] = new[] { "answers.displayName" },
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
                                DefaultValue = JsonSerializer.SerializeToElement("Ada"),
                                Constraints = new UserInputQuestionConstraints
                                {
                                    MinLength = 1,
                                    MaxLength = 80,
                                },
                            },
                            new UserInputQuestion
                            {
                                Id = "question.favorite-color",
                                Context = "Select the color for the report.",
                                Intent = "Set the report accent color.",
                                Prompt = "Which color should we use?",
                                ContextPath = "answers.favoriteColor",
                                Type = UserInputQuestionTypes.SingleChoice,
                                Required = true,
                                Options =
                                [
                                    new UserInputOption { Value = "blue", Label = "Blue" },
                                    new UserInputOption { Value = "green", Label = "Green" },
                                ],
                            },
                        ],
                    },
                ],
            },
        };
        var untypedTransition = new CommandTransition
        {
            Id = "transition.legacy-ask",
            StepKind = WorkflowStepKind.AskUser,
            TargetNodeId = "state.done",
        };
        var instance = new WorkflowInstance
        {
            InstanceId = "ask-user-contract",
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                ["state.done"] = new StateNode { Id = "state.done" },
                [typedTransition.Id] = typedTransition,
                [untypedTransition.Id] = untypedTransition,
            },
        };

        var json = WorkflowJsonSerializer.Serialize(instance);
        using var document = JsonDocument.Parse(json);
        var nodes = document.RootElement.GetProperty("nodes");
        var typedJson = nodes.GetProperty(typedTransition.Id).GetProperty("userInput");
        Assert.False(nodes.GetProperty(untypedTransition.Id).TryGetProperty("userInput", out _));
        Assert.Equal(1, typedJson.GetProperty("version").GetInt32());
        Assert.Equal("question.display-name", typedJson.GetProperty("questionGroups")[0].GetProperty("questions")[0].GetProperty("id").GetString());

        var roundTrip = WorkflowJsonSerializer.Deserialize(json);
        var typedRoundTrip = Assert.IsType<CommandTransition>(roundTrip.Nodes[typedTransition.Id]);
        var questions = Assert.Single(typedRoundTrip.UserInput!.QuestionGroups).Questions;
        var question = questions[0];
        var choiceQuestion = questions[1];
        Assert.Equal(UserInputQuestionTypes.Text, question.Type);
        Assert.Equal("answers.displayName", question.ContextPath);
        Assert.Equal("Ada", question.DefaultValue!.Value.GetString());
        Assert.Null(Assert.IsType<CommandTransition>(roundTrip.Nodes[untypedTransition.Id]).UserInput);

        var clone = WorkflowInstanceCloner.Clone(roundTrip);
        var clonedTransition = Assert.IsType<CommandTransition>(clone.Nodes[typedTransition.Id]);
        var clonedQuestions = Assert.Single(clonedTransition.UserInput!.QuestionGroups).Questions;
        var clonedQuestion = clonedQuestions[0];
        var clonedChoiceQuestion = clonedQuestions[1];
        Assert.NotSame(typedRoundTrip.UserInput, clonedTransition.UserInput);
        Assert.NotSame(typedRoundTrip.UserInput!.QuestionGroups, clonedTransition.UserInput.QuestionGroups);
        Assert.NotSame(question, clonedQuestion);
        Assert.NotSame(question.Constraints, clonedQuestion.Constraints);
        Assert.NotSame(question.Constraints!.AllowedMediaTypes, clonedQuestion.Constraints!.AllowedMediaTypes);
        Assert.NotSame(choiceQuestion.Options, clonedChoiceQuestion.Options);
        Assert.NotSame(choiceQuestion.Options[0], clonedChoiceQuestion.Options[0]);
        clonedChoiceQuestion.Options[0].Label = "Changed only in the clone.";
        Assert.Equal("Blue", choiceQuestion.Options[0].Label);
        clonedQuestion.Prompt = "Changed only in the clone.";
        Assert.Equal("What display name should we use?", question.Prompt);
        Assert.Equal("Ada", clonedQuestion.DefaultValue!.Value.GetString());

        var schema = WorkflowSchemaDemoExporter.CreateSchemaContract();
        Assert.Contains("userInput", schema.NodeFields[JsonPolymorphicConsts.CommandKind]);
        Assert.Equal(
            [
                UserInputQuestionTypes.SingleChoice,
                UserInputQuestionTypes.MultipleChoice,
                UserInputQuestionTypes.Text,
                UserInputQuestionTypes.Number,
                UserInputQuestionTypes.Boolean,
                UserInputQuestionTypes.File,
                UserInputQuestionTypes.Audio,
            ],
            schema.AllowedValues["userInputQuestionType"]);
    }
}
