namespace Techne.Loom.Abstractions.TaskTracking.Model;

public interface IWorkflowInstanceVisualizer
{
    Task<Stream> VisualizeAsync(WorkflowInstance instance, VisualizerLevel level = VisualizerLevel.Basic);

    Task VisualizeAsync(WorkflowInstance instance, Stream target, VisualizerLevel level = VisualizerLevel.Basic);

    Task<byte[]> VisualizeToByteArrayAsync(WorkflowInstance instance, VisualizerLevel level = VisualizerLevel.Basic);

    Task<string> VisualizeToBase64StringAsync(WorkflowInstance instance, VisualizerLevel level = VisualizerLevel.Basic);

    Task<string> VisualizeToStringAsync(WorkflowInstance instance, VisualizerLevel level = VisualizerLevel.Basic);
}
