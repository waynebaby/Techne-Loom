using System.Text;
using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.SkillOrchestrator.Visualizer;

public abstract class WorkflowInstanceVisualizerBase : IWorkflowInstanceVisualizer
{
    public async Task<Stream> VisualizeAsync(WorkflowInstance instance, VisualizerLevel level = VisualizerLevel.Basic)
    {
        var content = await VisualizeToStringAsync(instance, level).ConfigureAwait(false);
        return new MemoryStream(Encoding.UTF8.GetBytes(content));
    }

    public async Task VisualizeAsync(WorkflowInstance instance, Stream target, VisualizerLevel level = VisualizerLevel.Basic)
    {
        var content = await VisualizeToByteArrayAsync(instance, level).ConfigureAwait(false);
        await target.WriteAsync(content).ConfigureAwait(false);
    }

    public async Task<byte[]> VisualizeToByteArrayAsync(WorkflowInstance instance, VisualizerLevel level = VisualizerLevel.Basic)
    {
        var content = await VisualizeToStringAsync(instance, level).ConfigureAwait(false);
        return Encoding.UTF8.GetBytes(content);
    }

    public async Task<string> VisualizeToBase64StringAsync(WorkflowInstance instance, VisualizerLevel level = VisualizerLevel.Basic)
    {
        var bytes = await VisualizeToByteArrayAsync(instance, level).ConfigureAwait(false);
        return Convert.ToBase64String(bytes);
    }

    public abstract Task<string> VisualizeToStringAsync(WorkflowInstance instance, VisualizerLevel level = VisualizerLevel.Basic);
}
