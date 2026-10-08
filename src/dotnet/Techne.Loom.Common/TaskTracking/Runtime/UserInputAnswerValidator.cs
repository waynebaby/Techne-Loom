using System.Net.Http.Headers;
using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public sealed record UserInputAnswerValidationResult(
    IReadOnlyList<UserInputAnswerDiagnostic> Diagnostics,
    IReadOnlyList<AskScopedNormalizedAnswer> NormalizedAnswers)
{
    public bool IsValid => Diagnostics.Count == 0;
}

public static class UserInputAnswerValidator
{
    public static UserInputAnswerValidationResult Validate(
        UserInputContract contract,
        IReadOnlyDictionary<string, AskScopedAnswerValue> answers,
        IReadOnlyDictionary<string, AskScopedAttachmentMetadata>? attachments = null,
        bool requireComplete = true)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(answers);
        attachments ??= new Dictionary<string, AskScopedAttachmentMetadata>(StringComparer.Ordinal);

        var diagnostics = new List<UserInputAnswerDiagnostic>();
        var normalized = new List<AskScopedNormalizedAnswer>();
        var questionIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in contract.QuestionGroups ?? [])
        {
            if (group?.Questions is null)
            {
                continue;
            }

            foreach (var question in group.Questions)
            {
                if (question is null || string.IsNullOrWhiteSpace(question.Id))
                {
                    continue;
                }

                questionIds.Add(question.Id);
                var location = $"question:{question.Id}";
                if (!answers.TryGetValue(question.Id, out var answer) || answer is null)
                {
                    if (requireComplete)
                    {
                        Add(diagnostics, location, question.Required
                            ? "A required question must be answered."
                            : "An optional question must be explicitly skipped or answered.");
                    }

                    continue;
                }

                if (answer.AttachmentIds is null)
                {
                    Add(diagnostics, location, "Attachment ids cannot be null.");
                    continue;
                }

                if (answer.Skipped)
                {
                    if (question.Required)
                    {
                        Add(diagnostics, location, "A required question cannot be skipped.");
                        continue;
                    }

                    if (answer.Value is not null || answer.AttachmentIds is { Count: > 0 })
                    {
                        Add(diagnostics, location, "A skipped question cannot also contain an answer or attachment.");
                        continue;
                    }

                    normalized.Add(new AskScopedNormalizedAnswer(
                        question.Id,
                        question.ContextPath,
                        null,
                        Skipped: true,
                        []));
                    continue;
                }

                var selectedAttachments = ResolveAttachments(question, answer, attachments, location, diagnostics);
                var valid = question.Type switch
                {
                    UserInputQuestionTypes.SingleChoice => ValidateSingleChoice(question, answer, location, diagnostics),
                    UserInputQuestionTypes.MultipleChoice => ValidateMultipleChoice(question, answer, location, diagnostics),
                    UserInputQuestionTypes.Text => ValidateText(question, answer, location, diagnostics),
                    UserInputQuestionTypes.Number => ValidateNumber(question, answer, location, diagnostics),
                    UserInputQuestionTypes.Boolean => ValidateBoolean(question, answer, location, diagnostics),
                    UserInputQuestionTypes.File => ValidateAttachmentAnswer(question, answer, selectedAttachments, location, diagnostics, audio: false),
                    UserInputQuestionTypes.Audio => ValidateAttachmentAnswer(question, answer, selectedAttachments, location, diagnostics, audio: true),
                    _ => AddUnsupportedType(diagnostics, location, question.Type),
                };

                if (valid)
                {
                    normalized.Add(new AskScopedNormalizedAnswer(
                        question.Id,
                        question.ContextPath,
                        answer.Value?.Clone(),
                        Skipped: false,
                        selectedAttachments));
                }
            }
        }

        foreach (var answerId in answers.Keys)
        {
            if (!questionIds.Contains(answerId))
            {
                Add(diagnostics, $"question:{answerId}", "The answer references an unknown question id.");
            }
        }

        return new UserInputAnswerValidationResult(diagnostics, normalized);
    }

    private static bool ValidateSingleChoice(
        UserInputQuestion question,
        AskScopedAnswerValue answer,
        string location,
        List<UserInputAnswerDiagnostic> diagnostics)
    {
        if (answer.AttachmentIds.Count > 0
            || answer.Value is not JsonElement value
            || value.ValueKind != JsonValueKind.String)
        {
            Add(diagnostics, location, "A single-choice answer must be one option value string.");
            return false;
        }

        var selected = value.GetString() ?? string.Empty;
        if (question.Options?.Any(option => option is not null && string.Equals(option.Value, selected, StringComparison.Ordinal)) != true)
        {
            Add(diagnostics, location, "The selected value is not a declared option.");
            return false;
        }

        return true;
    }

    private static bool ValidateMultipleChoice(
        UserInputQuestion question,
        AskScopedAnswerValue answer,
        string location,
        List<UserInputAnswerDiagnostic> diagnostics)
    {
        if (answer.AttachmentIds.Count > 0
            || answer.Value is not JsonElement value
            || value.ValueKind != JsonValueKind.Array)
        {
            Add(diagnostics, location, "A multiple-choice answer must be an array of option value strings.");
            return false;
        }

        var selected = new List<string>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                Add(diagnostics, location, "A multiple-choice answer must contain only option value strings.");
                return false;
            }

            selected.Add(item.GetString() ?? string.Empty);
        }

        if (selected.Distinct(StringComparer.Ordinal).Count() != selected.Count)
        {
            Add(diagnostics, location, "A multiple-choice answer cannot repeat an option value.");
            return false;
        }

        var optionValues = question.Options?
            .Where(static option => option is not null)
            .Select(static option => option.Value)
            .ToHashSet(StringComparer.Ordinal) ?? [];
        if (selected.Any(item => !optionValues.Contains(item)))
        {
            Add(diagnostics, location, "A multiple-choice answer contains an undeclared option value.");
            return false;
        }

        if (!WithinBounds(selected.Count, question.Constraints?.MinSelections, question.Constraints?.MaxSelections))
        {
            Add(diagnostics, location, "The number of selected options is outside the configured bounds.");
            return false;
        }

        return true;
    }

    private static bool ValidateText(
        UserInputQuestion question,
        AskScopedAnswerValue answer,
        string location,
        List<UserInputAnswerDiagnostic> diagnostics)
    {
        if (answer.AttachmentIds.Count > 0
            || answer.Value is not JsonElement value
            || value.ValueKind != JsonValueKind.String)
        {
            Add(diagnostics, location, "A text answer must be a string.");
            return false;
        }

        var text = value.GetString() ?? string.Empty;
        if (!WithinBounds(text.Length, question.Constraints?.MinLength, question.Constraints?.MaxLength))
        {
            Add(diagnostics, location, "The text answer is outside the configured length bounds.");
            return false;
        }

        return true;
    }

    private static bool ValidateNumber(
        UserInputQuestion question,
        AskScopedAnswerValue answer,
        string location,
        List<UserInputAnswerDiagnostic> diagnostics)
    {
        if (answer.AttachmentIds.Count > 0
            || answer.Value is not JsonElement value
            || value.ValueKind != JsonValueKind.Number
            || !value.TryGetDecimal(out var number))
        {
            Add(diagnostics, location, "A number answer must be a finite JSON number.");
            return false;
        }

        if ((question.Constraints?.Minimum is decimal minimum && number < minimum)
            || (question.Constraints?.Maximum is decimal maximum && number > maximum))
        {
            Add(diagnostics, location, "The number answer is outside the configured bounds.");
            return false;
        }

        return true;
    }

    private static bool ValidateBoolean(
        UserInputQuestion question,
        AskScopedAnswerValue answer,
        string location,
        List<UserInputAnswerDiagnostic> diagnostics)
    {
        if (answer.AttachmentIds.Count > 0
            || answer.Value is not JsonElement value
            || value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            Add(diagnostics, location, "A boolean answer must be true or false.");
            return false;
        }

        return true;
    }

    private static List<AskScopedAttachmentMetadata> ResolveAttachments(
        UserInputQuestion question,
        AskScopedAnswerValue answer,
        IReadOnlyDictionary<string, AskScopedAttachmentMetadata> attachments,
        string location,
        List<UserInputAnswerDiagnostic> diagnostics)
    {
        var resolved = new List<AskScopedAttachmentMetadata>();
        if (answer.AttachmentIds is null)
        {
            Add(diagnostics, location, "Attachment ids cannot be null.");
            return resolved;
        }

        if (answer.AttachmentIds.Distinct(StringComparer.Ordinal).Count() != answer.AttachmentIds.Count)
        {
            Add(diagnostics, location, "An answer cannot reference an attachment more than once.");
            return resolved;
        }

        foreach (var attachmentId in answer.AttachmentIds)
        {
            if (!attachments.TryGetValue(attachmentId, out var metadata) || metadata is null)
            {
                Add(diagnostics, location, $"Attachment '{attachmentId}' is not part of this ask.");
                continue;
            }

            if (!string.Equals(metadata.QuestionId, question.Id, StringComparison.Ordinal))
            {
                Add(diagnostics, location, "An attachment can only be used by its designated question.");
                continue;
            }

            resolved.Add(metadata);
        }

        return resolved;
    }

    private static bool ValidateAttachmentAnswer(
        UserInputQuestion question,
        AskScopedAnswerValue answer,
        IReadOnlyList<AskScopedAttachmentMetadata> attachments,
        string location,
        List<UserInputAnswerDiagnostic> diagnostics,
        bool audio)
    {
        if (answer.Value is not null || attachments.Count != 1)
        {
            Add(diagnostics, location, audio
                ? "An audio answer must reference exactly one uploaded audio attachment."
                : "A file answer must reference exactly one uploaded attachment.");
            return false;
        }

        var attachment = attachments[0];
        if (attachment is null
            || attachment.Length <= 0
            || attachment.Sha256 is not { Length: 64 } sha256
            || sha256.Any(static character => !Uri.IsHexDigit(character))
            || !MediaTypeHeaderValue.TryParse(attachment.MediaType, out _))
        {
            Add(diagnostics, location, "The uploaded attachment metadata is invalid.");
            return false;
        }

        if (question.Constraints?.MaxAttachmentBytes is long maximumBytes && attachment.Length > maximumBytes)
        {
            Add(diagnostics, location, "The uploaded attachment exceeds the configured size limit.");
            return false;
        }

        if (audio && !MatchesMediaType(attachment.MediaType, "audio/*"))
        {
            Add(diagnostics, location, "An audio question accepts only audio media types.");
            return false;
        }

        var allowed = question.Constraints?.AllowedMediaTypes ?? [];
        if (allowed.Count > 0 && !allowed.Any(pattern => MatchesMediaType(attachment.MediaType, pattern)))
        {
            Add(diagnostics, location, "The uploaded attachment media type is not allowed.");
            return false;
        }

        return true;
    }

    private static bool MatchesMediaType(string mediaType, string pattern)
    {
        var wildcardSubtype = pattern.EndsWith("/*", StringComparison.Ordinal);
        var normalizedPattern = wildcardSubtype ? pattern[..^1] + "x" : pattern;
        if (!MediaTypeHeaderValue.TryParse(mediaType, out var mediaTypeHeader)
            || !MediaTypeHeaderValue.TryParse(normalizedPattern, out var patternHeader))
        {
            return false;
        }

        if (mediaTypeHeader.MediaType is not string mediaTypeValue
            || patternHeader.MediaType is not string patternValue)
        {
            return false;
        }

        var mediaParts = mediaTypeValue.Split('/');
        var patternParts = patternValue.Split('/');
        return mediaParts.Length == 2
            && patternParts.Length == 2
            && (string.Equals(patternParts[0], "*", StringComparison.Ordinal) || string.Equals(patternParts[0], mediaParts[0], StringComparison.OrdinalIgnoreCase))
            && (wildcardSubtype || string.Equals(patternParts[1], mediaParts[1], StringComparison.OrdinalIgnoreCase));
    }

    private static bool AddUnsupportedType(List<UserInputAnswerDiagnostic> diagnostics, string location, string? answerType)
    {
        Add(diagnostics, location, $"Answer type '{answerType}' is not supported.");
        return false;
    }

    private static bool WithinBounds(int value, int? minimum, int? maximum)
        => (minimum is null || value >= minimum.Value)
            && (maximum is null || value <= maximum.Value);

    private static void Add(List<UserInputAnswerDiagnostic> diagnostics, string location, string message)
        => diagnostics.Add(new UserInputAnswerDiagnostic(location, message));
}
