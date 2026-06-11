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

public enum ConcurrencyStrategy
{
    FirstSuccess,
    FirstResponse,
    All,
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
