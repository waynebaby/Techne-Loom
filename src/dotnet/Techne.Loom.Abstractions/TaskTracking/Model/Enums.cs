using System.Text.Json;
using System.Text.Json.Serialization;

namespace Techne.Loom.Abstractions.TaskTracking.Model;

public enum WorkflowStatus
{
    Drafting,
    ReadyToStart,
    Running,
    WaitingExternal,
    Failed,
    Succeeded,
}

public enum TaskNodeType
{
    State,
    Transition,
}

public enum ExecutionStatus
{
    Started,
    Succeeded,
    Failed,
    Skipped,
    Suspended,
}

[JsonConverter(typeof(ConcurrencyStrategyJsonConverter))]
public enum ConcurrencyStrategy
{
    FirstPass,
    WhenAnyResponse,
    WhenAnyPass,
    WhenAllPass,
    WhenExpression,
    LegacyFirstSuccess,
    LegacyFirstResponse,
    LegacyAll,
    FirstSuccess = FirstPass,
    FirstResponse = WhenAnyResponse,
    All = WhenAllPass,
}

public sealed class ConcurrencyStrategyJsonConverter : JsonConverter<ConcurrencyStrategy>
{
    public override ConcurrencyStrategy Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("Concurrency strategy must be a lower-camel-case string.");
        }

        return reader.GetString() switch
        {
            "firstPass" => ConcurrencyStrategy.FirstPass,
            "whenAnyResponse" => ConcurrencyStrategy.WhenAnyResponse,
            "whenAnyPass" => ConcurrencyStrategy.WhenAnyPass,
            "whenAllPass" => ConcurrencyStrategy.WhenAllPass,
            "whenExpression" => ConcurrencyStrategy.WhenExpression,
            "firstSuccess" => ConcurrencyStrategy.LegacyFirstSuccess,
            "firstResponse" => ConcurrencyStrategy.LegacyFirstResponse,
            "all" => ConcurrencyStrategy.LegacyAll,
            var value => throw new JsonException($"Unsupported concurrency strategy '{value}'. Use firstPass, whenAnyResponse, whenAnyPass, whenAllPass, or whenExpression."),
        };
    }

    public override void Write(Utf8JsonWriter writer, ConcurrencyStrategy value, JsonSerializerOptions options)
    {
        var name = value switch
        {
            ConcurrencyStrategy.FirstPass => "firstPass",
            ConcurrencyStrategy.WhenAnyResponse => "whenAnyResponse",
            ConcurrencyStrategy.WhenAnyPass => "whenAnyPass",
            ConcurrencyStrategy.WhenAllPass => "whenAllPass",
            ConcurrencyStrategy.WhenExpression => "whenExpression",
            ConcurrencyStrategy.LegacyFirstSuccess => "firstSuccess",
            ConcurrencyStrategy.LegacyFirstResponse => "firstResponse",
            ConcurrencyStrategy.LegacyAll => "all",
            _ => throw new JsonException($"Unsupported concurrency strategy value '{value}'."),
        };
        writer.WriteStringValue(name);
    }
}

public enum CommandInvocationKind
{
    CommandLine,
    NativeCode,
    Http,
    Tool,
    PythonScript,
}

public enum WorkflowStepKind
{
    ModelThink,
    Plan,
    ToolCall,
    McpCall,
    SubagentCall,
    AskUser,
    ConditionBranch,
    WaitResume,
    StateUpdate,
    ArtifactEmit,
    MemoryRead,
    MemoryWrite,
}

public enum WaitBehavior
{
    BlockUntilComplete,
    WaitForSignal,
}

public enum WorkflowInstanceVisualizerType
{
    Mermaid,
    Html,
    AsciiArt,
    Svg,
}

public enum VisualizerLevel
{
    Basic,
    Detailed,
    Full,
}
