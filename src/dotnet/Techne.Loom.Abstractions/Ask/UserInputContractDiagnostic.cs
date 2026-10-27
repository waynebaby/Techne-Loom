using System.Net.Http.Headers;
using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public sealed record UserInputContractDiagnostic(
    string TransitionId,
    string Location,
    string Message,
    string Suggestion);
