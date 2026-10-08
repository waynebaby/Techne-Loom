using System.Text.Json;

namespace Techne.Loom.Abstractions.TaskTracking.Model;

public sealed class UserInputContract
{
    public int Version { get; set; } = 1;

    public List<UserInputQuestionGroup> QuestionGroups { get; set; } = [];
}

public sealed class UserInputQuestionGroup
{
    public string Id { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public List<UserInputQuestion> Questions { get; set; } = [];
}

public sealed class UserInputQuestion
{
    public string Id { get; set; } = string.Empty;

    public string Context { get; set; } = string.Empty;

    public string Intent { get; set; } = string.Empty;

    public string Prompt { get; set; } = string.Empty;

    public string ContextPath { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public bool Required { get; set; }

    public List<UserInputOption> Options { get; set; } = [];

    public bool? Multiple { get; set; }

    public JsonElement? DefaultValue { get; set; }

    public string? HelpText { get; set; }

    public UserInputQuestionConstraints? Constraints { get; set; }
}

public sealed class UserInputOption
{
    public string Value { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;
}

public sealed class UserInputQuestionConstraints
{
    public int? MinLength { get; set; }

    public int? MaxLength { get; set; }

    public decimal? Minimum { get; set; }

    public decimal? Maximum { get; set; }

    public int? MinSelections { get; set; }

    public int? MaxSelections { get; set; }

    public List<string> AllowedMediaTypes { get; set; } = [];

    public long? MaxAttachmentBytes { get; set; }
}

public static class UserInputQuestionTypes
{
    public const string SingleChoice = "singleChoice";

    public const string MultipleChoice = "multipleChoice";

    public const string Text = "text";

    public const string Number = "number";

    public const string Boolean = "boolean";

    public const string File = "file";

    public const string Audio = "audio";
}
