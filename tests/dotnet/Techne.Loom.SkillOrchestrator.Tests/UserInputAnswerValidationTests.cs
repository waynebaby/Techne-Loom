using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class UserInputAnswerValidationTests
{
    [Fact]
    public void Validate_AcceptsAllAnswerTypesAndValidSha256AttachmentsInContractOrder()
    {
        var contract = CreateContract(
            Question("single", UserInputQuestionTypes.SingleChoice, options: ["red", "blue"]),
            Question("multiple", UserInputQuestionTypes.MultipleChoice, options: ["a", "b", "c"], constraints: new UserInputQuestionConstraints { MinSelections = 1, MaxSelections = 2 }),
            Question("text", UserInputQuestionTypes.Text, constraints: new UserInputQuestionConstraints { MinLength = 1, MaxLength = 20 }),
            Question("number", UserInputQuestionTypes.Number, constraints: new UserInputQuestionConstraints { Minimum = 1, Maximum = 10 }),
            Question("boolean", UserInputQuestionTypes.Boolean),
            Question("file", UserInputQuestionTypes.File, required: true),
            Question("audio", UserInputQuestionTypes.Audio, required: true));
        var answers = new Dictionary<string, AskScopedAnswerValue>(StringComparer.Ordinal)
        {
            ["single"] = Answer(JsonSerializer.SerializeToElement("blue")),
            ["multiple"] = Answer(JsonSerializer.SerializeToElement(new[] { "a", "b" })),
            ["text"] = Answer(JsonSerializer.SerializeToElement("Ada")),
            ["number"] = Answer(JsonSerializer.SerializeToElement(5)),
            ["boolean"] = Answer(JsonSerializer.SerializeToElement(true)),
            ["file"] = Answer(attachmentIds: ["file-1"]),
            ["audio"] = Answer(attachmentIds: ["audio-1"]),
        };
        var attachments = new Dictionary<string, AskScopedAttachmentMetadata>(StringComparer.Ordinal)
        {
            ["file-1"] = Attachment("file-1", "file", "application/pdf"),
            ["audio-1"] = Attachment("audio-1", "audio", "audio/wav"),
        };

        var result = UserInputAnswerValidator.Validate(contract, answers, attachments);

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Diagnostics.Select(static item => item.Message)));
        Assert.Equal(["single", "multiple", "text", "number", "boolean", "file", "audio"], result.NormalizedAnswers.Select(static item => item.QuestionId));
        Assert.Equal(64, result.NormalizedAnswers[^1].Attachments[0].Sha256.Length);
    }

    [Fact]
    public void Validate_RequiresExplicitOptionalSkipAndDoesNotUseDefaultAsAnswer()
    {
        var required = Question("required", UserInputQuestionTypes.Text, required: true);
        required.DefaultValue = JsonSerializer.SerializeToElement("prefill only");
        var contract = CreateContract(required, Question("optional", UserInputQuestionTypes.Boolean));

        var missing = UserInputAnswerValidator.Validate(contract, new Dictionary<string, AskScopedAnswerValue>(StringComparer.Ordinal));
        Assert.Equal(2, missing.Diagnostics.Count);

        var answersWithInvalidRequiredSkip = UserInputAnswerValidator.Validate(
            contract,
            new Dictionary<string, AskScopedAnswerValue>(StringComparer.Ordinal)
            {
                ["required"] = new AskScopedAnswerValue { Skipped = true },
                ["optional"] = new AskScopedAnswerValue { Skipped = true },
            });
        Assert.False(answersWithInvalidRequiredSkip.IsValid);
        Assert.Contains(answersWithInvalidRequiredSkip.Diagnostics, static item => item.Location == "question:required");
    }

    [Fact]
    public void Validate_RejectsOutOfRangeValuesUnknownQuestionsAndMismatchedAttachments()
    {
        var contract = CreateContract(
            Question("choice", UserInputQuestionTypes.SingleChoice, options: ["yes", "no"]),
            Question("text", UserInputQuestionTypes.Text, constraints: new UserInputQuestionConstraints { MinLength = 3 }),
            Question("file", UserInputQuestionTypes.File, required: true));
        var result = UserInputAnswerValidator.Validate(
            contract,
            new Dictionary<string, AskScopedAnswerValue>(StringComparer.Ordinal)
            {
                ["choice"] = Answer(JsonSerializer.SerializeToElement("maybe")),
                ["text"] = Answer(JsonSerializer.SerializeToElement("x")),
                ["file"] = Answer(attachmentIds: ["unknown-file"]),
                ["unknown-question"] = Answer(JsonSerializer.SerializeToElement(true)),
            });

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, static item => item.Message.Contains("declared option", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, static item => item.Message.Contains("length bounds", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, static item => item.Message.Contains("not part of this ask", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, static item => item.Message.Contains("unknown question", StringComparison.Ordinal));

        var mismatched = UserInputAnswerValidator.Validate(
            contract,
            new Dictionary<string, AskScopedAnswerValue>(StringComparer.Ordinal)
            {
                ["file"] = Answer(attachmentIds: ["file-mismatch"]),
            },
            new Dictionary<string, AskScopedAttachmentMetadata>(StringComparer.Ordinal)
            {
                ["file-mismatch"] = Attachment("file-mismatch", "choice", "application/pdf"),
            },
            requireComplete: false);
        Assert.Contains(mismatched.Diagnostics, static item => item.Message.Contains("designated question", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_ReportsNullAttachmentCollectionsAndMetadata()
    {
        var contract = CreateContract(
            Question("text", UserInputQuestionTypes.Text),
            Question("file", UserInputQuestionTypes.File));
        var nullAttachmentIds = UserInputAnswerValidator.Validate(
            contract,
            new Dictionary<string, AskScopedAnswerValue>(StringComparer.Ordinal)
            {
                ["text"] = new AskScopedAnswerValue
                {
                    Value = JsonSerializer.SerializeToElement("answer"),
                    AttachmentIds = null!,
                },
            },
            requireComplete: false);
        Assert.Contains(nullAttachmentIds.Diagnostics, static item => item.Message.Contains("ids cannot be null", StringComparison.Ordinal));

        var nullMetadata = UserInputAnswerValidator.Validate(
            contract,
            new Dictionary<string, AskScopedAnswerValue>(StringComparer.Ordinal)
            {
                ["file"] = Answer(attachmentIds: ["null-metadata"]),
            },
            new Dictionary<string, AskScopedAttachmentMetadata>(StringComparer.Ordinal)
            {
                ["null-metadata"] = null!,
            },
            requireComplete: false);
        Assert.Contains(nullMetadata.Diagnostics, static item => item.Message.Contains("not part of this ask", StringComparison.Ordinal));

        var nullDigest = UserInputAnswerValidator.Validate(
            contract,
            new Dictionary<string, AskScopedAnswerValue>(StringComparer.Ordinal)
            {
                ["file"] = Answer(attachmentIds: ["null-digest"]),
            },
            new Dictionary<string, AskScopedAttachmentMetadata>(StringComparer.Ordinal)
            {
                ["null-digest"] = new AskScopedAttachmentMetadata(
                    "null-digest",
                    "file",
                    "file.pdf",
                    "application/pdf",
                    1,
                    null!,
                    DateTimeOffset.UtcNow),
            },
            requireComplete: false);
        Assert.Contains(nullDigest.Diagnostics, static item => item.Message.Contains("metadata is invalid", StringComparison.Ordinal));
    }

    private static UserInputContract CreateContract(params UserInputQuestion[] questions)
        => new()
        {
            QuestionGroups =
            [
                new UserInputQuestionGroup
                {
                    Id = "group.answers",
                    Title = "Answers",
                    Questions = [.. questions],
                },
            ],
        };

    private static UserInputQuestion Question(
        string id,
        string type,
        bool required = false,
        string[]? options = null,
        UserInputQuestionConstraints? constraints = null)
        => new()
        {
            Id = id,
            Context = "Need this answer for the request.",
            Intent = "Support the workflow request.",
            Prompt = $"Provide {id}.",
            ContextPath = $"answers.{id}",
            Type = type,
            Required = required,
            Options = options?.Select(static value => new UserInputOption { Value = value, Label = value }).ToList() ?? [],
            Constraints = constraints,
        };

    private static AskScopedAnswerValue Answer(JsonElement? value = null, List<string>? attachmentIds = null)
        => new() { Value = value, AttachmentIds = attachmentIds ?? [] };

    private static AskScopedAttachmentMetadata Attachment(string id, string questionId, string mediaType)
        => new(id, questionId, $"{id}.bin", mediaType, 16, new string('a', 64), DateTimeOffset.UtcNow);
}
