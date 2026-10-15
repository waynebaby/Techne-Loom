using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class ExpressionContractJsonTests
{
    [Theory]
    [InlineData(ConcurrencyStrategy.FirstPass, "firstPass")]
    [InlineData(ConcurrencyStrategy.WhenAnyResponse, "whenAnyResponse")]
    [InlineData(ConcurrencyStrategy.WhenAnyPass, "whenAnyPass")]
    [InlineData(ConcurrencyStrategy.WhenAllPass, "whenAllPass")]
    [InlineData(ConcurrencyStrategy.WhenExpression, "whenExpression")]
    public void ConcurrencyStrategy_UsesCanonicalWireValues(ConcurrencyStrategy strategy, string expected)
    {
        var options = WorkflowJsonSerializer.CreateDefaultOptions(indented: false);

        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(strategy, options));
    }

    [Theory]
    [InlineData("firstSuccess", ConcurrencyStrategy.LegacyFirstSuccess)]
    [InlineData("firstResponse", ConcurrencyStrategy.LegacyFirstResponse)]
    [InlineData("all", ConcurrencyStrategy.LegacyAll)]
    public void ConcurrencyStrategy_LegacyValuesRoundTripForMigrationDiagnostics(string legacyName, ConcurrencyStrategy expected)
    {
        var options = WorkflowJsonSerializer.CreateDefaultOptions(indented: false);
        var parsed = JsonSerializer.Deserialize<ConcurrencyStrategy>(JsonSerializer.Serialize(legacyName), options);

        Assert.Equal(expected, parsed);
        Assert.Equal(JsonSerializer.Serialize(legacyName), JsonSerializer.Serialize(parsed, options));
    }

    [Fact]
    public void WorkflowJsonSerializer_MarksLegacyFieldsForExplicitMigration()
    {
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            Groups = [new TransitionGroup { Id = "group.start", TransitionIds = ["transition.command"] }],
        };
        var end = new StateNode { Id = "state.end", Name = "End", Groups = [] };
        var command = new CommandTransition
        {
            Id = "transition.command",
            Name = "Command",
            TargetNodeId = end.Id,
            Command = new CommandInvocation { Kind = CommandInvocationKind.Tool, Name = "noop" },
        };
        var instance = new WorkflowInstance
        {
            InstanceId = "legacy-contract",
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [end.Id] = end,
                [command.Id] = command,
            },
        };
        var root = System.Text.Json.Nodes.JsonNode.Parse(WorkflowJsonSerializer.Serialize(instance))!.AsObject();
        var stateGroup = root["nodes"]![start.Id]!["groups"]![0]!.AsObject();
        stateGroup["strategy"] = "firstSuccess";
        var rawCommand = root["nodes"]![command.Id]!.AsObject();
        rawCommand["succeedExpression"] = rawCommand["passExpression"]!.DeepClone();
        rawCommand.Remove("passExpression");
        rawCommand["maxRetry"] = 2;
        rawCommand.Remove("maxRetries");

        var migrated = WorkflowJsonSerializer.Deserialize(root.ToJsonString());
        var parsedCommand = Assert.IsType<CommandTransition>(migrated.Nodes[command.Id]);

        Assert.Equal(ConcurrencyStrategy.LegacyFirstSuccess, migrated.GetStateNodes()[start.Id].Groups[0].Strategy);
        Assert.True(parsedCommand.LegacySucceedExpressionWasDeclared);
        Assert.False(parsedCommand.PassExpressionWasExplicitlyDeclared);
        Assert.True(parsedCommand.LegacyMaxRetryWasDeclared);
    }

    [Fact]
    public void StringShorthand_ReadsAndAlwaysWritesStructuredDefinition()
    {
        const string json = "{\"guardExpression\":\"approved == true\"}";
        var options = WorkflowJsonSerializer.CreateDefaultOptions(indented: false);

        var value = JsonSerializer.Deserialize<Dictionary<string, ExpressionDefinition>>(json, options);
        var serialized = JsonSerializer.Serialize(value, options);

        Assert.NotNull(value);
        Assert.Equal("approved == true", value!["guardExpression"].Source);
        Assert.Contains("\"kind\":\"predicate\"", serialized, StringComparison.Ordinal);
        Assert.Contains("\"source\":\"approved == true\"", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("\"guardExpression\":\"approved == true\"", serialized, StringComparison.Ordinal);
    }

    [Fact]
    public void CompileFeedback_UsesSnakeCaseForEveryNestedWireField()
    {
        var feedback = new WorkflowCompileFeedback
        {
            Product = "skill-orchestrator",
            Runtime = "dotnet-so",
            Status = "failed",
            WorkflowPath = "C:/workflow.json",
            WorkflowHash = "workflow-hash",
            CandidatePath = "C:/candidate.json",
            CandidateHash = "candidate-hash",
            RuntimeIdentity = "Techne.Loom.SkillOrchestrator",
            RuntimeVersion = "0.3.258-beta",
            Counts = new WorkflowCompileFeedbackCounts
            {
                Total = 1,
                Errors = 1,
                Warnings = 0,
                Info = 0,
                Blocked = 0,
            },
            Diagnostics =
            [
                new WorkflowCompileDiagnostic
                {
                    RuleId = "SO3002",
                    Code = "SO3002",
                    Category = "semantic",
                    Severity = "error",
                    Message = "invalid expression",
                    Location = "transition-1/guardExpression",
                    SuggestedFix = "Fix the expression.",
                    Phase = "expressions",
                    BlockedBy = ["structure"],
                    ExpressionFeedback = new ExpressionCompileFeedback
                    {
                        Status = "failed",
                        Language = "csharp",
                        LanguageVersion = "12",
                        ContractId = "loom.expression.csharp",
                        ContractVersion = "1",
                        WorkflowId = "workflow-1",
                        GateId = "gate-1",
                        TransitionId = "transition-1",
                        Field = "transition-1/guardExpression",
                        SourceSpan = new ExpressionSourceSpan
                        {
                            StartLine = 1,
                            StartColumn = 2,
                            EndLine = 1,
                            EndColumn = 8,
                        },
                        DiagnosticCode = "LOOM.EXPR.CSHARP.CS1002",
                        DiagnosticCategory = "syntax",
                        Severity = "error",
                        Message = "semicolon expected",
                        SuggestedFix = "Add the missing semicolon.",
                        ReferencedSymbols = ["context"],
                        CompilerIdentity = "Microsoft.CodeAnalysis.CSharp",
                        Kind = "predicate",
                        EntryPoint = "Evaluate",
                        ResultType = "bool",
                        Capabilities = ["context.read"],
                        Warnings = ["warning"],
                        Diagnostics =
                        [
                            new ExpressionCompileDiagnostic
                            {
                                Code = "LOOM.EXPR.CSHARP.CS1002",
                                Category = "syntax",
                                Severity = "error",
                                Message = "semicolon expected",
                                SourceSpan = new ExpressionSourceSpan
                                {
                                    StartLine = 1,
                                    StartColumn = 2,
                                    EndLine = 1,
                                    EndColumn = 8,
                                },
                                SuggestedFix = "Add the missing semicolon.",
                            },
                        ],
                        Truncated = true,
                        DiagnosticCount = 1,
                    },
                },
            ],
            Phases =
            [
                new WorkflowCompilePhaseFeedback
                {
                    Name = "expressions",
                    Status = "failed",
                    Prerequisites = ["structure"],
                    DiagnosticCount = 1,
                    BlockedBy = [],
                },
            ],
            Truncated = true,
        };
        using var document = JsonDocument.Parse(
            JsonSerializer.Serialize(feedback, WorkflowJsonSerializer.CreateDefaultOptions()));
        var root = document.RootElement;
        foreach (var propertyName in new[]
        {
            "schema_version", "product", "runtime", "status", "workflow_path", "workflow_hash",
            "candidate_path", "candidate_hash", "runtime_identity", "runtime_version", "counts",
            "diagnostics", "phases", "truncated",
        })
        {
            Assert.True(root.TryGetProperty(propertyName, out _), propertyName);
        }
        var nested = root.GetProperty("diagnostics")[0].GetProperty("expression_feedback");
        foreach (var propertyName in new[]
        {
            "status", "language", "language_version", "contract_id", "contract_version", "workflow_id",
            "gate_id", "transition_id", "field", "source_span", "diagnostic_code", "diagnostic_category",
            "severity", "message", "suggested_fix", "referenced_symbols", "compiler_identity", "kind",
            "entry_point", "result_type", "capabilities", "warnings", "diagnostics", "truncated", "diagnostic_count",
        })
        {
            Assert.True(nested.TryGetProperty(propertyName, out _), propertyName);
        }
        var span = nested.GetProperty("source_span");
        foreach (var propertyName in new[] { "start_line", "start_column", "end_line", "end_column" })
        {
            Assert.True(span.TryGetProperty(propertyName, out _), propertyName);
        }
        var nestedDiagnostic = nested.GetProperty("diagnostics")[0];
        foreach (var propertyName in new[] { "code", "category", "severity", "message", "source_span", "suggested_fix" })
        {
            Assert.True(nestedDiagnostic.TryGetProperty(propertyName, out _), propertyName);
        }
        Assert.False(nested.TryGetProperty("languageVersion", out _));
        Assert.False(nested.TryGetProperty("diagnosticCode", out _));
        Assert.False(span.TryGetProperty("startLine", out _));
    }

    [Fact]
    public void Clone_CopiesRootBindingAndExpressionDefinitions()
    {
        var source = new WorkflowInstance
        {
            RuntimeBinding = "dotnet-so",
            ExpressionBinding = new ExpressionBinding
            {
                Language = "csharp",
                LanguageVersion = "12",
                ContractId = "loom.expression.csharp",
                ContractVersion = "1",
                RequiredExpressionCapabilities = ["context.read"],
                CompileFeedbackContract = "detailedCompileFeedbackV1",
            },
            Validation = new WorkflowValidationContract
            {
                Gates = new Dictionary<string, WorkflowValidationGate>(StringComparer.Ordinal)
                {
                    ["gate"] = new() { PassExpression = new ExpressionDefinition { Source = "gate_outputs_present" } },
                },
            },
        };

        var clone = WorkflowInstanceCloner.Clone(source);

        Assert.Equal(source.RuntimeBinding, clone.RuntimeBinding);
        Assert.Equal(source.ExpressionBinding.Language, clone.ExpressionBinding.Language);
        Assert.Equal(source.ExpressionBinding.RequiredExpressionCapabilities, clone.ExpressionBinding.RequiredExpressionCapabilities);
        Assert.NotSame(source.ExpressionBinding, clone.ExpressionBinding);
        Assert.NotSame(source.ExpressionBinding.RequiredExpressionCapabilities, clone.ExpressionBinding.RequiredExpressionCapabilities);
        Assert.NotNull(clone.Validation);
        Assert.NotSame(source.Validation!.Gates["gate"].PassExpression, clone.Validation!.Gates["gate"].PassExpression);
        Assert.Equal("gate_outputs_present", clone.Validation.Gates["gate"].PassExpression!.Source);
    }
}
