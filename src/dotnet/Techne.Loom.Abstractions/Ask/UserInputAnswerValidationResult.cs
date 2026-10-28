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
