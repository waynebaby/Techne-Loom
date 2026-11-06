
public static class WorkflowDemoVerifier

{

    public static WorkflowScriptVerificationResult Verify(

        WorkflowInstance actual,

        WorkflowInstance reference,

        WorkflowModelReference model)

    {

        var suite = new WorkflowScriptVerificationSuite();

        var transitions = actual.GetTransitionNodes().Values;

        suite.Check("demo.schema", model.SchemaId == "techne-loom.workflow-instance", "The candidate uses the current workflow schema.", "demo");

        suite.Check("demo.runtime_binding", actual.RuntimeBinding == model.RuntimeBinding, "The candidate uses the selected runtime binding.", "demo");

        suite.Check("demo.memory_read", transitions.Any(item => item.StepKind == WorkflowStepKind.MemoryRead), "The demo contains a MemoryRead step.", "demo");

        suite.Check("demo.state_update", transitions.Any(item => item.StepKind == WorkflowStepKind.StateUpdate), "The demo contains a StateUpdate step.", "demo");

        suite.Check("demo.memory_write", transitions.Any(item => item.StepKind == WorkflowStepKind.MemoryWrite), "The demo contains a MemoryWrite step.", "demo");

        suite.Check("demo.tool_projection", transitions.OfType<CommandTransition>().Any(item => item.StepKind == WorkflowStepKind.ToolCall && item.OutputPath == "tool.result"), "The tool step has an output path.", "demo");

        suite.Check("demo.canonical_resume", transitions.OfType<CommandTransition>().Where(item => item.StepKind == WorkflowStepKind.ModelThink || item.StepKind == WorkflowStepKind.WaitResume).All(item => item.Command.Parameters is not null && item.Command.Parameters.ContainsKey("resumeOutputKey") && item.Command.Parameters.ContainsKey("projectionMode")), "External steps declare canonical resume projection fields.", "demo");

        suite.Check("demo.blocked_route", transitions.Any(item => item.BlockedRoutes?.Contains("blocked") == true), "The demo includes a blocked route.", "demo");

        suite.Check("demo.rework_loop", actual.Nodes.ContainsKey("transition.rework") && actual.Nodes.ContainsKey("transition.reset_rework"), "The demo includes a rework loop.", "demo");

        suite.Check("demo.artifact", transitions.OfType<CommandTransition>().Any(item => item.StepKind == WorkflowStepKind.ArtifactEmit), "The demo emits an artifact.", "demo");

        suite.Check("demo.gates", actual.Validation?.Gates.ContainsKey("gate.final") == true && actual.Validation.Gates.ContainsKey("gate.blocked"), "The demo contains terminal and blocked gates.", "demo");

        suite.Check("demo.reference_shape", actual.Nodes.Count == reference.Nodes.Count, "The candidate and reference have the same node count.", "demo", reference.Nodes.Count.ToString(), actual.Nodes.Count.ToString());

        return suite.Complete(null, "workflow.demo.json");

    }

}
