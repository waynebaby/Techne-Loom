namespace Techne.Loom.Common.TaskTracking.Runtime;

public sealed class WorkflowAuditDeliveryException : InvalidOperationException
{
    public WorkflowAuditDeliveryException(
        string message,
        WorkflowAuditArtifacts auditArtifacts,
        Exception? innerException = null)
        : base(message, innerException)
    {
        AuditArtifacts = auditArtifacts;
    }

    public WorkflowAuditArtifacts AuditArtifacts { get; }
}
