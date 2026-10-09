using System.Net.Http.Headers;
using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public sealed record UserInputContractDiagnostic(
    string TransitionId,
    string Location,
    string Message,
    string Suggestion);

public static class UserInputContractValidator
{
    private const int SupportedVersion = 1;

    public static IReadOnlyList<UserInputContractDiagnostic> Validate(WorkflowInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        return Validate(instance.GetTransitionNodes().Values);
    }

    public static IReadOnlyList<UserInputContractDiagnostic> Validate(IEnumerable<TransitionBase> transitions)
    {
        ArgumentNullException.ThrowIfNull(transitions);
        var diagnostics = new List<UserInputContractDiagnostic>();
        foreach (var commandTransition in transitions.OfType<CommandTransition>())
        {
            var contract = commandTransition.UserInput;
            if (contract is null)
            {
                continue;
            }

            if (commandTransition.StepKind != WorkflowStepKind.AskUser)
            {
                Add(
                    diagnostics,
                    commandTransition,
                    "userInput",
                    $"Transition '{commandTransition.Id}' declares a user input contract but its stepKind is '{commandTransition.StepKind}'.",
                    "Declare userInput only on an AskUser transition.");
                continue;
            }

            if (contract.Version != SupportedVersion)
            {
                Add(
                    diagnostics,
                    commandTransition,
                    "userInput/version",
                    $"Transition '{commandTransition.Id}' uses unsupported user input contract version '{contract.Version}'.",
                    $"Set userInput.version to {SupportedVersion}.");
                continue;
            }

            ValidateContract(commandTransition, contract, diagnostics);
        }

        return diagnostics;
    }

    private static void ValidateContract(
        CommandTransition transition,
        UserInputContract contract,
        List<UserInputContractDiagnostic> diagnostics)
    {
        if (contract.QuestionGroups is null || contract.QuestionGroups.Count == 0)
        {
            Add(
                diagnostics,
                transition,
                "userInput/questionGroups",
                $"AskUser transition '{transition.Id}' must declare at least one question group.",
                "Add an ordered question group containing at least one question.");
            return;
        }

        var requiredInputs = GetRequiredInputs(transition.Command?.Parameters);
        var groupIds = new HashSet<string>(StringComparer.Ordinal);
        var questionIds = new HashSet<string>(StringComparer.Ordinal);
        var contextPaths = new HashSet<string>(StringComparer.Ordinal);

        for (var groupIndex = 0; groupIndex < contract.QuestionGroups.Count; groupIndex++)
        {
            var group = contract.QuestionGroups[groupIndex];
            var groupPath = $"userInput/questionGroups/{groupIndex}";
            if (group is null)
            {
                Add(diagnostics, transition, groupPath, "A question group cannot be null.", "Replace the null group with a group containing an id and questions.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(group.Id))
            {
                Add(diagnostics, transition, $"{groupPath}/id", "A question group must have a non-empty id.", "Assign a stable, unique group id.");
            }
            else if (!groupIds.Add(group.Id))
            {
                Add(diagnostics, transition, $"{groupPath}/id", $"Question group id '{group.Id}' is duplicated.", "Use a unique id for each question group.");
            }

            if (string.IsNullOrWhiteSpace(group.Title))
            {
                Add(diagnostics, transition, $"{groupPath}/title", "A question group must have a non-empty title.", "Provide a short user-facing group title.");
            }

            if (group.Questions is null || group.Questions.Count == 0)
            {
                Add(diagnostics, transition, $"{groupPath}/questions", "A question group must contain at least one question.", "Add an ordered question to the group.");
                continue;
            }

            for (var questionIndex = 0; questionIndex < group.Questions.Count; questionIndex++)
            {
                var question = group.Questions[questionIndex];
                var questionPath = $"{groupPath}/questions/{questionIndex}";
                if (question is null)
                {
                    Add(diagnostics, transition, questionPath, "A question cannot be null.", "Replace the null item with a complete question.");
                    continue;
                }

                ValidateQuestion(
                    transition,
                    question,
                    questionPath,
                    requiredInputs,
                    questionIds,
                    contextPaths,
                    diagnostics);
            }
        }
    }

    private static void ValidateQuestion(
        CommandTransition transition,
        UserInputQuestion question,
        string questionPath,
        HashSet<string> requiredInputs,
        HashSet<string> questionIds,
        HashSet<string> contextPaths,
        List<UserInputContractDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(question.Id))
        {
            Add(diagnostics, transition, $"{questionPath}/id", "A question must have a non-empty id.", "Assign a stable, unique question id.");
        }
        else if (!questionIds.Add(question.Id))
        {
            Add(diagnostics, transition, $"{questionPath}/id", $"Question id '{question.Id}' is duplicated.", "Use a unique id for each question across all groups.");
        }

        if (string.IsNullOrWhiteSpace(question.Context))
        {
            Add(diagnostics, transition, $"{questionPath}/context", "A question must describe the context for the user.", "Provide a concise user-facing context.");
        }

        if (string.IsNullOrWhiteSpace(question.Intent))
        {
            Add(diagnostics, transition, $"{questionPath}/intent", "A question must state its intent.", "Explain why the answer is needed.");
        }

        if (string.IsNullOrWhiteSpace(question.Prompt))
        {
            Add(diagnostics, transition, $"{questionPath}/prompt", "A question must have a user-facing prompt.", "Provide the question shown to the user.");
        }

        if (string.IsNullOrWhiteSpace(question.ContextPath))
        {
            Add(diagnostics, transition, $"{questionPath}/contextPath", "A question must bind its answer to a context path.", "Set contextPath to a declared user input path.");
        }
        else
        {
            if (!contextPaths.Add(question.ContextPath))
            {
                Add(diagnostics, transition, $"{questionPath}/contextPath", $"Context path '{question.ContextPath}' is bound by more than one question.", "Bind each question to a distinct context path.");
            }

            if (!requiredInputs.Contains(question.ContextPath))
            {
                Add(diagnostics, transition, $"{questionPath}/contextPath", $"Context path '{question.ContextPath}' is not declared in requiredInputs.", "Add the same context path to the AskUser command's requiredInputs.");
            }
        }

        switch (question.Type)
        {
            case UserInputQuestionTypes.SingleChoice:
                ValidateChoiceQuestion(transition, question, questionPath, multiple: false, diagnostics);
                break;
            case UserInputQuestionTypes.MultipleChoice:
                ValidateChoiceQuestion(transition, question, questionPath, multiple: true, diagnostics);
                break;
            case UserInputQuestionTypes.Text:
                ValidateNonChoiceQuestion(transition, question, questionPath, diagnostics);
                ValidateConstraintShape(transition, question, questionPath, diagnostics, allowText: true);
                ValidateTextBounds(transition, question, questionPath, diagnostics);
                ValidateTextDefault(transition, question, questionPath, diagnostics);
                break;
            case UserInputQuestionTypes.Number:
                ValidateNonChoiceQuestion(transition, question, questionPath, diagnostics);
                ValidateConstraintShape(transition, question, questionPath, diagnostics, allowNumber: true);
                ValidateNumberBounds(transition, question, questionPath, diagnostics);
                ValidateNumberDefault(transition, question, questionPath, diagnostics);
                break;
            case UserInputQuestionTypes.Boolean:
                ValidateNonChoiceQuestion(transition, question, questionPath, diagnostics);
                ValidateConstraintShape(transition, question, questionPath, diagnostics);
                ValidateBooleanDefault(transition, question, questionPath, diagnostics);
                break;
            case UserInputQuestionTypes.File:
            case UserInputQuestionTypes.Audio:
                ValidateNonChoiceQuestion(transition, question, questionPath, diagnostics);
                ValidateConstraintShape(transition, question, questionPath, diagnostics, allowAttachment: true);
                ValidateAttachmentConstraints(transition, question, questionPath, diagnostics);
                ValidateAttachmentDefault(transition, question, questionPath, diagnostics);
                break;
            default:
                Add(
                    diagnostics,
                    transition,
                    $"{questionPath}/type",
                    $"Question '{question.Id}' uses unsupported answer type '{question.Type}'.",
                    "Use singleChoice, multipleChoice, text, number, boolean, file, or audio.");
                break;
        }
    }

    private static void ValidateChoiceQuestion(
        CommandTransition transition,
        UserInputQuestion question,
        string questionPath,
        bool multiple,
        List<UserInputContractDiagnostic> diagnostics)
    {
        if (question.Multiple is bool declaredMultiple && declaredMultiple != multiple)
        {
            Add(diagnostics, transition, $"{questionPath}/multiple", "The multiple flag conflicts with the question type.", multiple
                ? "Set multiple to true or omit it for multipleChoice."
                : "Set multiple to false or omit it for singleChoice.");
        }

        ValidateConstraintShape(transition, question, questionPath, diagnostics, allowSelection: true);
        ValidateSelectionBounds(transition, question, questionPath, diagnostics);

        var optionValues = new HashSet<string>(StringComparer.Ordinal);
        foreach (var option in question.Options ?? [])
        {
            if (option is null)
            {
                Add(diagnostics, transition, $"{questionPath}/options", "A choice option cannot be null.", "Replace null options with a value and label.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(option.Value))
            {
                Add(diagnostics, transition, $"{questionPath}/options", "A choice option must have a non-empty value.", "Assign a stable option value.");
            }
            else if (!optionValues.Add(option.Value))
            {
                Add(diagnostics, transition, $"{questionPath}/options", $"Choice option value '{option.Value}' is duplicated.", "Use unique option values within a question.");
            }

            if (string.IsNullOrWhiteSpace(option.Label))
            {
                Add(diagnostics, transition, $"{questionPath}/options", "A choice option must have a non-empty label.", "Provide a user-facing option label.");
            }
        }

        var availableChoiceCount = optionValues.Count + 1;
        if ((question.Constraints?.MinSelections is int minimumSelections && minimumSelections > availableChoiceCount)
            || (question.Constraints?.MaxSelections is int maximumSelections && maximumSelections > availableChoiceCount))
        {
            Add(diagnostics, transition, $"{questionPath}/constraints", "Selection bounds cannot exceed the number of distinct options plus Other.", "Reduce minSelections or maxSelections to the number of declared options plus one for Other.");
        }

        if (!multiple && (question.Constraints?.MinSelections > 1 || question.Constraints?.MaxSelections > 1))
        {
            Add(diagnostics, transition, $"{questionPath}/constraints", "A single-choice question cannot select more than one option.", "Remove selection bounds above one or use multipleChoice.");
        }

        ValidateChoiceDefault(transition, question, questionPath, multiple, optionValues, diagnostics);
    }

    private static void ValidateNonChoiceQuestion(
        CommandTransition transition,
        UserInputQuestion question,
        string questionPath,
        List<UserInputContractDiagnostic> diagnostics)
    {
        if (question.Options is { Count: > 0 })
        {
            Add(diagnostics, transition, $"{questionPath}/options", "Only a choice question may declare options.", "Remove options or use a choice question type.");
        }

        if (question.Multiple is not null)
        {
            Add(diagnostics, transition, $"{questionPath}/multiple", "Only a choice question may declare multiple.", "Remove multiple from this question.");
        }
    }

    private static void ValidateConstraintShape(
        CommandTransition transition,
        UserInputQuestion question,
        string questionPath,
        List<UserInputContractDiagnostic> diagnostics,
        bool allowText = false,
        bool allowNumber = false,
        bool allowSelection = false,
        bool allowAttachment = false)
    {
        var constraints = question.Constraints;
        if (constraints is null)
        {
            return;
        }

        var hasText = constraints.MinLength is not null || constraints.MaxLength is not null;
        var hasNumber = constraints.Minimum is not null || constraints.Maximum is not null;
        var hasSelection = constraints.MinSelections is not null || constraints.MaxSelections is not null;
        var hasAttachment = constraints.MaxAttachmentBytes is not null || constraints.AllowedMediaTypes is { Count: > 0 };
        if ((!allowText && hasText)
            || (!allowNumber && hasNumber)
            || (!allowSelection && hasSelection)
            || (!allowAttachment && hasAttachment))
        {
            Add(diagnostics, transition, $"{questionPath}/constraints", "The question declares constraints that do not apply to its answer type.", "Keep only constraints supported by this question type.");
        }
    }

    private static void ValidateTextBounds(
        CommandTransition transition,
        UserInputQuestion question,
        string questionPath,
        List<UserInputContractDiagnostic> diagnostics)
    {
        var constraints = question.Constraints;
        if (constraints?.MinLength < 0 || constraints?.MaxLength < 0
            || (constraints?.MinLength is int minimum && constraints.MaxLength is int maximum && minimum > maximum))
        {
            Add(diagnostics, transition, $"{questionPath}/constraints", "Text length bounds must be non-negative and minimum cannot exceed maximum.", "Set valid minLength and maxLength values.");
        }
    }

    private static void ValidateNumberBounds(
        CommandTransition transition,
        UserInputQuestion question,
        string questionPath,
        List<UserInputContractDiagnostic> diagnostics)
    {
        var constraints = question.Constraints;
        if (constraints?.Minimum is decimal minimum && constraints.Maximum is decimal maximum && minimum > maximum)
        {
            Add(diagnostics, transition, $"{questionPath}/constraints", "The numeric minimum cannot exceed its maximum.", "Set a minimum no greater than the maximum.");
        }
    }

    private static void ValidateSelectionBounds(
        CommandTransition transition,
        UserInputQuestion question,
        string questionPath,
        List<UserInputContractDiagnostic> diagnostics)
    {
        var constraints = question.Constraints;
        if (constraints?.MinSelections < 0
            || constraints?.MaxSelections < 1
            || (constraints?.MinSelections is int minimum && constraints.MaxSelections is int maximum && minimum > maximum))
        {
            Add(diagnostics, transition, $"{questionPath}/constraints", "Selection bounds must be non-negative, have a positive maximum, and minimum cannot exceed maximum.", "Set valid minSelections and maxSelections values.");
        }
    }

    private static void ValidateAttachmentConstraints(
        CommandTransition transition,
        UserInputQuestion question,
        string questionPath,
        List<UserInputContractDiagnostic> diagnostics)
    {
        var constraints = question.Constraints;
        if (constraints is null)
        {
            return;
        }

        if (constraints.MaxAttachmentBytes is <= 0)
        {
            Add(diagnostics, transition, $"{questionPath}/constraints/maxAttachmentBytes", "Maximum attachment size must be positive.", "Set maxAttachmentBytes to a positive byte count or omit it.");
        }

        if (constraints.AllowedMediaTypes is null)
        {
            Add(diagnostics, transition, $"{questionPath}/constraints/allowedMediaTypes", "Allowed media types cannot be null.", "Provide a list of valid media types or omit the list.");
            return;
        }

        var mediaTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var mediaType in constraints.AllowedMediaTypes)
        {
            if (string.IsNullOrWhiteSpace(mediaType) || !MediaTypeHeaderValue.TryParse(mediaType, out _))
            {
                Add(diagnostics, transition, $"{questionPath}/constraints/allowedMediaTypes", $"Media type '{mediaType}' is invalid.", "Use valid MIME media types such as audio/wav.");
            }
            else if (!mediaTypes.Add(mediaType))
            {
                Add(diagnostics, transition, $"{questionPath}/constraints/allowedMediaTypes", $"Media type '{mediaType}' is duplicated.", "List each allowed media type once.");
            }
        }
    }

    private static void ValidateChoiceDefault(
        CommandTransition transition,
        UserInputQuestion question,
        string questionPath,
        bool multiple,
        HashSet<string> optionValues,
        List<UserInputContractDiagnostic> diagnostics)
    {
        if (question.DefaultValue is not JsonElement defaultValue)
        {
            return;
        }

        var values = new List<string>();
        if (!multiple && defaultValue.ValueKind == JsonValueKind.String)
        {
            values.Add(defaultValue.GetString()!);
        }
        else if (multiple && defaultValue.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in defaultValue.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String)
                {
                    Add(diagnostics, transition, $"{questionPath}/defaultValue", "A multiple-choice default must contain only string option values.", "Choose default values from the declared options.");
                    return;
                }

                values.Add(item.GetString()!);
            }
        }
        else
        {
            Add(diagnostics, transition, $"{questionPath}/defaultValue", "The default value shape does not match the choice question type.", multiple
                ? "Use an array of option values for multipleChoice."
                : "Use one option value string for singleChoice.");
            return;
        }

        if (values.Any(value => !optionValues.Contains(value))
            || values.Distinct(StringComparer.Ordinal).Count() != values.Count)
        {
            Add(diagnostics, transition, $"{questionPath}/defaultValue", "The choice default contains an unknown or repeated option value.", "Use unique values declared in options.");
        }

        var constraints = question.Constraints;
        if ((constraints?.MinSelections is int minimum && values.Count < minimum)
            || (constraints?.MaxSelections is int maximum && values.Count > maximum))
        {
            Add(diagnostics, transition, $"{questionPath}/defaultValue", "The choice default is outside the selection bounds.", "Choose a default that satisfies minSelections and maxSelections.");
        }
    }

    private static void ValidateTextDefault(
        CommandTransition transition,
        UserInputQuestion question,
        string questionPath,
        List<UserInputContractDiagnostic> diagnostics)
    {
        if (question.DefaultValue is not JsonElement defaultValue)
        {
            return;
        }

        if (defaultValue.ValueKind != JsonValueKind.String)
        {
            Add(diagnostics, transition, $"{questionPath}/defaultValue", "A text question default must be a string.", "Provide a string default value.");
            return;
        }

        var length = defaultValue.GetString()!.Length;
        if ((question.Constraints?.MinLength is int minimum && length < minimum)
            || (question.Constraints?.MaxLength is int maximum && length > maximum))
        {
            Add(diagnostics, transition, $"{questionPath}/defaultValue", "The text default is outside the length bounds.", "Choose a default that satisfies minLength and maxLength.");
        }
    }

    private static void ValidateNumberDefault(
        CommandTransition transition,
        UserInputQuestion question,
        string questionPath,
        List<UserInputContractDiagnostic> diagnostics)
    {
        if (question.DefaultValue is not JsonElement defaultValue)
        {
            return;
        }

        if (defaultValue.ValueKind != JsonValueKind.Number || !defaultValue.TryGetDecimal(out var value))
        {
            Add(diagnostics, transition, $"{questionPath}/defaultValue", "A number question default must be a finite JSON number.", "Provide a numeric default value.");
            return;
        }

        if ((question.Constraints?.Minimum is decimal minimum && value < minimum)
            || (question.Constraints?.Maximum is decimal maximum && value > maximum))
        {
            Add(diagnostics, transition, $"{questionPath}/defaultValue", "The numeric default is outside the configured bounds.", "Choose a default that satisfies minimum and maximum.");
        }
    }

    private static void ValidateBooleanDefault(
        CommandTransition transition,
        UserInputQuestion question,
        string questionPath,
        List<UserInputContractDiagnostic> diagnostics)
    {
        if (question.DefaultValue is JsonElement defaultValue && defaultValue.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            Add(diagnostics, transition, $"{questionPath}/defaultValue", "A boolean question default must be true or false.", "Provide a boolean default value.");
        }
    }

    private static void ValidateAttachmentDefault(
        CommandTransition transition,
        UserInputQuestion question,
        string questionPath,
        List<UserInputContractDiagnostic> diagnostics)
    {
        if (question.DefaultValue is not null)
        {
            Add(diagnostics, transition, $"{questionPath}/defaultValue", "File and audio questions cannot declare a default attachment.", "Remove defaultValue and let the user provide the attachment.");
        }
    }

    private static HashSet<string> GetRequiredInputs(Dictionary<string, object?>? parameters)
    {
        var paths = new HashSet<string>(StringComparer.Ordinal);
        if (parameters?.TryGetValue("requiredInputs", out var value) != true)
        {
            return paths;
        }

        if (value is JsonElement { ValueKind: JsonValueKind.Array } jsonArray)
        {
            foreach (var item in jsonArray.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                {
                    paths.Add(item.GetString()!);
                }
            }

            return paths;
        }

        if (value is IEnumerable<string> strings)
        {
            paths.UnionWith(strings.Where(static path => !string.IsNullOrWhiteSpace(path)));
            return paths;
        }

        if (value is IEnumerable<object?> items)
        {
            foreach (var item in items)
            {
                if (item is string path && !string.IsNullOrWhiteSpace(path))
                {
                    paths.Add(path);
                }
                else if (item is JsonElement { ValueKind: JsonValueKind.String } jsonPath
                    && !string.IsNullOrWhiteSpace(jsonPath.GetString()))
                {
                    paths.Add(jsonPath.GetString()!);
                }
            }
        }

        return paths;
    }

    private static void Add(
        List<UserInputContractDiagnostic> diagnostics,
        CommandTransition transition,
        string locationSuffix,
        string message,
        string suggestion)
    {
        diagnostics.Add(new UserInputContractDiagnostic(
            transition.Id,
            $"transition:{transition.Id}/{locationSuffix}",
            message,
            suggestion));
    }
}
