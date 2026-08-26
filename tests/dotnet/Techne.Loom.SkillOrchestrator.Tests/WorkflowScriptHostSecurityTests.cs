using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class WorkflowScriptHostSecurityTests
{
    [Fact]
    public async Task FullyQualifiedFileApi_IsRejectedByWorkflowScriptHost()
    {
        var scriptFile = Path.Combine(Path.GetTempPath(), $"loom-script-security-{Guid.NewGuid():N}.cs");
        await File.WriteAllTextAsync(
            scriptFile,
            "public static class S { public static WorkflowInstance Build(WorkflowScriptInput input) => new WorkflowInstance { RuntimeBinding = input.RuntimeBinding, RuntimeVersion = input.RuntimeVersion, Context = new Dictionary<string, object?> { [\"value\"] = System.IO.File.ReadAllText(\"missing.txt\") } }; }");

        try
        {
            var host = new WorkflowScriptHost();
            var execution = await host.ExecuteBuilderAsync(
                scriptFile,
                new WorkflowScriptInput { RuntimeBinding = "dotnet-so", RuntimeVersion = "0.1.0" });

            Assert.False(execution.IsSuccess);
            Assert.Equal("LOOM.SCRIPT.SECURITY.UNAPPROVED_API", execution.Feedback.DiagnosticCode);
            Assert.Contains("File", execution.Feedback.Error, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(scriptFile))
            {
                File.Delete(scriptFile);
            }
        }
    }
    [Fact]
    public async Task ReflectionChain_IsRejectedByWorkflowScriptHost()
    {
        var scriptFile = Path.Combine(Path.GetTempPath(), $"loom-script-reflection-{Guid.NewGuid():N}.cs");
        await File.WriteAllTextAsync(
            scriptFile,
            "public static class S { public static WorkflowInstance Build(WorkflowScriptInput input) { var method = new object().GetType().GetMethod(\"ToString\"); _ = method?.Invoke(new object(), null); return new WorkflowInstance { RuntimeBinding = input.RuntimeBinding, RuntimeVersion = input.RuntimeVersion }; } }");

        try
        {
            var host = new WorkflowScriptHost();
            var execution = await host.ExecuteBuilderAsync(
                scriptFile,
                new WorkflowScriptInput { RuntimeBinding = "dotnet-so", RuntimeVersion = "0.1.0" });

            Assert.False(execution.IsSuccess);
            Assert.Equal("LOOM.SCRIPT.SECURITY.UNAPPROVED_API", execution.Feedback.DiagnosticCode);
            Assert.Contains("not allowed", execution.Feedback.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(scriptFile))
            {
                File.Delete(scriptFile);
            }
        }
    }

    [Fact]
    public async Task ModelEvidenceReferencePathRead_IsAllowedByWorkflowScriptHost()
    {
        var scriptFile = Path.Combine(Path.GetTempPath(), $"loom-script-model-read-{Guid.NewGuid():N}.cs");
        await File.WriteAllTextAsync(
            scriptFile,
            "public static class S { public static WorkflowScriptVerificationResult Verify(WorkflowInstance actual, WorkflowInstance reference, WorkflowModelReference model) { var path = reference.Validation!.Gates[\"gate.final\"].FailureGuidance!.EvidenceReferences[0].Path; return new WorkflowScriptVerificationResult { Passed = path == \"evidence.md\" }; } }");

        var reference = new WorkflowInstance
        {
            Validation = new WorkflowValidationContract
            {
                Gates = new Dictionary<string, WorkflowValidationGate>(StringComparer.Ordinal)
                {
                    ["gate.final"] = new WorkflowValidationGate
                    {
                        FailureGuidance = new WorkflowGateFailureGuidance
                        {
                            EvidenceReferences = [new WorkflowEvidenceReference { Path = "evidence.md" }],
                        },
                    },
                },
            },
        };

        try
        {
            var host = new WorkflowScriptHost();
            var execution = await host.ExecuteVerifierAsync(
                scriptFile,
                new WorkflowInstance(),
                reference,
                new WorkflowModelReference());

            Assert.True(execution.IsSuccess, execution.Feedback.Error);
            Assert.True(execution.Value!.Passed);
        }
        finally
        {
            if (File.Exists(scriptFile))
            {
                File.Delete(scriptFile);
            }
        }
    }
}
