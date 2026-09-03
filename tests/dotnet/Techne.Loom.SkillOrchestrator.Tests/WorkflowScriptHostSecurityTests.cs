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
    [Fact]
    public async Task ReadOnlyJsonAndInMemoryHashingAreAllowedByWorkflowScriptHost()
    {
        var source = "public static class S { public static WorkflowInstance Build(WorkflowScriptInput input) { var payload = (JsonElement)input.Context[\"payload\"]!; var status = payload.GetProperty(\"status\").GetString() ?? string.Empty; var bytes = Encoding.UTF8.GetBytes(status); var hash = SHA256.HashData(bytes); var hex = Convert.ToHexString(hash); var roundTrip = Convert.ToBase64String(Convert.FromHexString(hex)); return new WorkflowInstance { RuntimeBinding = input.RuntimeBinding, RuntimeVersion = roundTrip }; } }";
        var payload = System.Text.Json.JsonSerializer.SerializeToElement(new { status = "ready" });
        var execution = await ExecuteBuilderAsync(source, new Dictionary<string, object?> { ["payload"] = payload });

        Assert.True(execution.IsSuccess, execution.Feedback.Error);
        var expected = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("ready")));
        Assert.Equal(expected, execution.Value!.RuntimeVersion);
    }

    [Fact]
    public async Task StaticRegexWithTimeoutIsAllowedByWorkflowScriptHost()
    {
        var source = "public static class S { public static WorkflowInstance Build(WorkflowScriptInput input) { var matched = Regex.Match(input.RuntimeVersion ?? string.Empty, \"^0[.]\", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)).Success; return new WorkflowInstance { RuntimeBinding = input.RuntimeBinding, RuntimeVersion = matched ? input.RuntimeVersion : null }; } }";
        var execution = await ExecuteBuilderAsync(source, new Dictionary<string, object?>());

        Assert.True(execution.IsSuccess, execution.Feedback.Error);
        Assert.Equal("0.3.270", execution.Value!.RuntimeVersion);
    }

    [Fact]
    public async Task StaticRegexReplaceWithTimeoutIsAllowedByWorkflowScriptHost()
    {
        var source = "public static class S { public static WorkflowInstance Build(WorkflowScriptInput input) { var replaced = Regex.Replace(input.RuntimeVersion ?? string.Empty, \"0\", \"x\", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)); return new WorkflowInstance { RuntimeBinding = input.RuntimeBinding, RuntimeVersion = replaced }; } }";
        var execution = await ExecuteBuilderAsync(source, new Dictionary<string, object?>());

        Assert.True(execution.IsSuccess, execution.Feedback.Error);
        Assert.Equal("x.3.27x", execution.Value!.RuntimeVersion);
    }

    [Fact]
    public async Task ScriptSecurityRejectsUnboundedAndSideEffectApis()
    {
        var sources = new[]
        {
            "public static class S { public static WorkflowInstance Build(WorkflowScriptInput input) => new WorkflowInstance { RuntimeBinding = input.RuntimeBinding, RuntimeVersion = Regex.IsMatch(input.RuntimeVersion ?? string.Empty, \"a+\") ? \"matched\" : \"not-matched\" }; }",
            "public static class S { public static WorkflowInstance Build(WorkflowScriptInput input) { var regex = new Regex(\"a+\"); return new WorkflowInstance { RuntimeBinding = input.RuntimeBinding, RuntimeVersion = regex.Match(input.RuntimeVersion ?? string.Empty).Value }; } }",
            "public static class S { public static WorkflowInstance Build(WorkflowScriptInput input) { var json = JsonSerializer.Serialize(input.Context); return new WorkflowInstance { RuntimeBinding = input.RuntimeBinding, RuntimeVersion = json }; } }",
            "public static class S { public static WorkflowInstance Build(WorkflowScriptInput input) { var value = System.IO.File.ReadAllText(\"missing.txt\"); return new WorkflowInstance { RuntimeBinding = input.RuntimeBinding, RuntimeVersion = value }; } }",
        };

        foreach (var source in sources)
        {
            var execution = await ExecuteBuilderAsync(source, new Dictionary<string, object?>());

            Assert.False(execution.IsSuccess);
            Assert.Equal("LOOM.SCRIPT.SECURITY.UNAPPROVED_API", execution.Feedback.DiagnosticCode);
            Assert.Contains("not allowed", execution.Feedback.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task ScriptRejectsNonUtf8EncodingAndNonDeterministicApis()
    {
        var cases = new[]
        {
            ("public static class S { public static WorkflowInstance Build(WorkflowScriptInput input) { var bytes = Encoding.ASCII.GetBytes(input.RuntimeVersion ?? string.Empty); return new WorkflowInstance { RuntimeBinding = input.RuntimeBinding, RuntimeVersion = Convert.ToBase64String(bytes) }; } }", "Encoding operations are allowed only through Encoding.UTF8."),
            ("public static class S { public static WorkflowInstance Build(WorkflowScriptInput input) => new WorkflowInstance { RuntimeBinding = input.RuntimeBinding, RuntimeVersion = Guid.NewGuid().ToString() }; }", "NewGuid"),
            ("public static class S { public static WorkflowInstance Build(WorkflowScriptInput input) { var random = new Random(); return new WorkflowInstance { RuntimeBinding = input.RuntimeBinding, RuntimeVersion = random.Next().ToString() }; } }", "Random"),
        };

        foreach (var (source, expectedMessage) in cases)
        {
            var execution = await ExecuteBuilderAsync(source, new Dictionary<string, object?>());

            Assert.False(execution.IsSuccess);
            Assert.Equal("LOOM.SCRIPT.SECURITY.UNAPPROVED_API", execution.Feedback.DiagnosticCode);
            Assert.Contains(expectedMessage, execution.Feedback.Error ?? string.Empty, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task ScriptSecurityRejectsGarbageCollectionApi()
    {
        var source = "public static class S { public static WorkflowInstance Build(WorkflowScriptInput input) { System.GC.Collect(); return new WorkflowInstance { RuntimeBinding = input.RuntimeBinding }; } }";
        var execution = await ExecuteBuilderAsync(source, new Dictionary<string, object?>());

        Assert.False(execution.IsSuccess);
        Assert.Equal("LOOM.SCRIPT.SECURITY.UNAPPROVED_API", execution.Feedback.DiagnosticCode);
        Assert.Contains("Collect", execution.Feedback.Error ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ScriptSecurityRejectsUncataloguedCoreApi()
    {
        var source = "public static class S { public static WorkflowInstance Build(WorkflowScriptInput input) { var memory = System.GC.GetTotalMemory(false); return new WorkflowInstance { RuntimeBinding = input.RuntimeBinding }; } }";
        var execution = await ExecuteBuilderAsync(source, new Dictionary<string, object?>());

        Assert.False(execution.IsSuccess);
        Assert.Equal("LOOM.SCRIPT.SECURITY.UNAPPROVED_API", execution.Feedback.DiagnosticCode);
        Assert.Contains("GetTotalMemory", execution.Feedback.Error ?? string.Empty, StringComparison.Ordinal);
    }

    private static async Task<WorkflowScriptExecution<WorkflowInstance>> ExecuteBuilderAsync(
        string source,
        IReadOnlyDictionary<string, object?> context)
    {
        var scriptFile = Path.Combine(Path.GetTempPath(), $"loom-script-capability-{Guid.NewGuid():N}.cs");
        await File.WriteAllTextAsync(scriptFile, source);
        try
        {
            return await new WorkflowScriptHost().ExecuteBuilderAsync(
                scriptFile,
                new WorkflowScriptInput
                {
                    RuntimeBinding = "dotnet-so",
                    RuntimeVersion = "0.3.270",
                    Context = context,
                });
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
