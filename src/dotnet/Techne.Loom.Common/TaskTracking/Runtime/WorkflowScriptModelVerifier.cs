using System.Globalization;
using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public static class WorkflowScriptModelVerifier
{
    private const string CanonicalProjection = "canonical";
    private const string LegacyNestedProjection = "legacyNested";
    private const string ContextPrefix = "$context:";
    private static readonly JsonSerializerOptions JsonOptions = WorkflowJsonSerializer.CreateDefaultOptions(indented: false);

    public static WorkflowScriptVerificationResult Verify(
        WorkflowInstance actual,
        WorkflowInstance reference,
        WorkflowModelReference model,
        string? actualJson = null,
        string? referenceJson = null)
    {
        ArgumentNullException.ThrowIfNull(actual);
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(model);

        var suite = new WorkflowScriptVerificationSuite();
        var runtimeEvidenceObserved = HasRuntimeEvidence(actual);
        var persistedRuntimeEvidencePresent = HasPersistedRuntimeEvidence(actual);
        var normalizedDiff = new Dictionary<string, object?>(StringComparer.Ordinal);
        using var actualDocument = ParseDocumentOrNull(actualJson, actual, suite, "actual");
        using var referenceDocument = ParseDocumentOrNull(referenceJson, reference, suite, "reference");
        VerifyRuntimeProvenance(runtimeEvidenceObserved, persistedRuntimeEvidencePresent, suite);

        VerifyRuntimeBinding(actual, model, suite);
        VerifyRuntimeVersion(actual, model, suite);
        VerifySerializerRoundTrip(actual, suite);
        VerifyRootFields(actual, model, actualDocument, suite);
        VerifyNodeFields(actual, model, actualDocument, suite);
        VerifyEnumValues(actual, model, actualDocument, suite);
        VerifyExpressions(actual, suite);
        VerifyGraphReferences(actual, suite);
        VerifyProjections(actual, suite);
        VerifyOutputFamilyProducers(actual, suite);
        VerifyGateValueSemantics(actual, suite);
        VerifyReferenceStructure(actual, reference, normalizedDiff, suite);
        VerifyRuntimeUpdates(actual, runtimeEvidenceObserved, suite);
        VerifyRuntimeArtifacts(actual, runtimeEvidenceObserved, suite);
        VerifyRuntimeGateEvidence(actual, runtimeEvidenceObserved, suite);
        VerifyBlockedAndTerminalEvidence(actual, runtimeEvidenceObserved, suite);

        var result = suite.Complete(normalizedDiff);
        result.RuntimeEvidenceObserved = runtimeEvidenceObserved;
        return result;
    }

    private static JsonDocument? ParseDocumentOrNull(
        string? json,
        WorkflowInstance fallback,
        WorkflowScriptVerificationSuite suite,
        string label)
    {
        var source = string.IsNullOrWhiteSpace(json) ? WorkflowJsonSerializer.Serialize(fallback, indented: false) : json;
        try
        {
            suite.Check(
                $"json.parse.{label}",
                true,
                "The workflow is a single JSON object.",
                "structure",
                "JSON object",
                "JSON object");
            return JsonDocument.Parse(source);
        }
        catch (JsonException exception)
        {
            suite.Check(
                $"json.parse.{label}",
                false,
                exception.Message,
                "structure",
                "JSON object",
                "invalid JSON");
            return null;
        }
    }

    private static void VerifyRuntimeBinding(
        WorkflowInstance actual,
        WorkflowModelReference model,
        WorkflowScriptVerificationSuite suite)
    {
        var passed = !string.IsNullOrWhiteSpace(actual.RuntimeBinding)
            && !string.IsNullOrWhiteSpace(model.RuntimeBinding)
            && string.Equals(actual.RuntimeBinding, model.RuntimeBinding, StringComparison.Ordinal);
        suite.Check(
            "runtime.binding",
            passed,
            passed ? "The workflow runtime binding matches the verifier model." : "The workflow runtime binding does not match the verifier model.",
            "runtime",
            model.RuntimeBinding,
            actual.RuntimeBinding);
    }

    private static void VerifyRuntimeVersion(

        WorkflowInstance actual,

        WorkflowModelReference model,

        WorkflowScriptVerificationSuite suite)

    {

        if (string.IsNullOrWhiteSpace(model.RuntimeVersion))

        {

            suite.Skip("runtime.version", "No exact runtime version was supplied to this verifier model.", "runtime");

            return;

        }



        var actualVersion = actual.RuntimeVersion

            ?? GetContextString(actual.Context, "runtimeVersion")

            ?? GetContextString(actual.Context, "runtime_version");

        var passed = !string.IsNullOrWhiteSpace(actualVersion)

            && string.Equals(actualVersion, model.RuntimeVersion, StringComparison.Ordinal);

        suite.Check(

            "runtime.version",

            passed,

            passed ? "The workflow runtime version matches the verifier model." : "The workflow must explicitly carry the exact runtime version used to generate it.",

            "runtime",

            model.RuntimeVersion,

            actualVersion ?? "missing");

    }

    private static void VerifySerializerRoundTrip(WorkflowInstance actual, WorkflowScriptVerificationSuite suite)
    {
        try
        {
            var roundTrip = WorkflowJsonSerializer.Deserialize(WorkflowJsonSerializer.Serialize(actual, indented: false));
            var passed = roundTrip.Nodes.Count == actual.Nodes.Count
                && roundTrip.Nodes.Keys.OrderBy(static value => value, StringComparer.Ordinal)
                    .SequenceEqual(actual.Nodes.Keys.OrderBy(static value => value, StringComparer.Ordinal), StringComparer.Ordinal);
            suite.Check(
                "serializer.round_trip",
                passed,
                passed ? "The workflow survives the runtime JSON round-trip." : "The workflow changed during the runtime JSON round-trip.",
                "structure",
                actual.Nodes.Count.ToString(CultureInfo.InvariantCulture),
                roundTrip.Nodes.Count.ToString(CultureInfo.InvariantCulture));
        }
        catch (Exception exception)
        {
            suite.Check("serializer.round_trip", false, exception.Message, "structure", "readable workflow model", "round-trip failed");
        }
    }

    private static void VerifyRootFields(
        WorkflowInstance actual,
        WorkflowModelReference model,
        JsonDocument? actualDocument,
        WorkflowScriptVerificationSuite suite)
    {
        var root = actualDocument?.RootElement;
        var requiredFields = model.RequiredRootFields.Count > 0
            ? model.RequiredRootFields
            : ["instanceId", "nodes", "startNodeId", "currentNodeId", "status", "context", "history", "version", "activeWaitGroups"];
        foreach (var field in requiredFields.Distinct(StringComparer.Ordinal))
        {
            var present = root is { ValueKind: JsonValueKind.Object } && root.Value.TryGetProperty(field, out _);
            var valueValid = field switch
            {
                "instanceId" => !string.IsNullOrWhiteSpace(actual.InstanceId),
                "nodes" => actual.Nodes is not null,
                "startNodeId" => !string.IsNullOrWhiteSpace(actual.StartNodeId),
                "currentNodeId" => !string.IsNullOrWhiteSpace(actual.CurrentNodeId),
                _ => true,
            };
            suite.Check(
                $"root.required.{field}",
                present && valueValid,
                present && valueValid ? "The required root field is present." : "The required root field is missing or empty.",
                "structure",
                "present",
                present && valueValid ? "present" : "missing-or-empty");
        }
    }

    private static void VerifyNodeFields(
        WorkflowInstance actual,
        WorkflowModelReference model,
        JsonDocument? actualDocument,
        WorkflowScriptVerificationSuite suite)
    {
        if (actualDocument?.RootElement.TryGetProperty("nodes", out var rawNodes) != true || rawNodes.ValueKind != JsonValueKind.Object)
        {
            suite.Check("nodes.discriminator", false, "The workflow nodes object is missing.", "structure", "object", "missing");
            return;
        }

        foreach (var rawNode in rawNodes.EnumerateObject())
        {
            var id = rawNode.Name;
            var rawKind = rawNode.Value.ValueKind == JsonValueKind.Object
                && rawNode.Value.TryGetProperty("$kind", out var kindProperty)
                && kindProperty.ValueKind == JsonValueKind.String
                ? kindProperty.GetString()
                : null;
            var knownKind = rawKind is "state" or "command" or "expr" or "tbr";
            var actualKind = actual.Nodes.TryGetValue(id, out var typedNode) ? GetNodeKind(typedNode) : null;
            var discriminatorPassed = knownKind && string.Equals(rawKind, actualKind, StringComparison.Ordinal);
            suite.Check(
                $"nodes.{id}.kind",
                discriminatorPassed,
                discriminatorPassed ? "The node discriminator matches the typed node." : "The node is missing a valid $kind discriminator or it disagrees with the typed model.",
                "structure",
                actualKind,
                rawKind ?? "missing");

            if (rawKind is null || !model.RequiredNodeFields.TryGetValue(rawKind, out var requiredFields))
            {
                continue;
            }

            foreach (var field in requiredFields.Distinct(StringComparer.Ordinal))
            {
                var present = rawNode.Value.ValueKind == JsonValueKind.Object && rawNode.Value.TryGetProperty(field, out _);
                suite.Check(
                    $"nodes.{id}.required.{field}",
                    present,
                    present ? "The required node field is present." : "The required node field is missing.",
                    "structure",
                    "present",
                    present ? "present" : "missing");
            }
        }
    }

    private static void VerifyEnumValues(
        WorkflowInstance actual,
        WorkflowModelReference model,
        JsonDocument? actualDocument,
        WorkflowScriptVerificationSuite suite)
    {
        CheckEnumProperty(actualDocument?.RootElement, "status", "workflowStatus", model, suite, "root.status");
        if (actualDocument?.RootElement.TryGetProperty("nodes", out var rawNodes) != true || rawNodes.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var rawNode in rawNodes.EnumerateObject())
        {
            CheckEnumProperty(rawNode.Value, "waitBehavior", "waitBehavior", model, suite, $"nodes.{rawNode.Name}.waitBehavior");
            CheckEnumProperty(rawNode.Value, "stepKind", "workflowStepKind", model, suite, $"nodes.{rawNode.Name}.stepKind");
            if (rawNode.Value.TryGetProperty("command", out var command) && command.ValueKind == JsonValueKind.Object)
            {
                CheckEnumProperty(command, "kind", "commandInvocationKind", model, suite, $"nodes.{rawNode.Name}.command.kind");
            }
        }
    }

    private static void CheckEnumProperty(
        JsonElement? parent,
        string propertyName,
        string allowedValuesKey,
        WorkflowModelReference model,
        WorkflowScriptVerificationSuite suite,
        string checkId)
    {
        if (parent is not { ValueKind: JsonValueKind.Object } value || !value.TryGetProperty(propertyName, out var property))
        {
            return;
        }

        var actual = property.ValueKind == JsonValueKind.String ? property.GetString() : property.GetRawText();
        var allowed = model.AllowedValues.TryGetValue(allowedValuesKey, out var values) ? values : [];
        var passed = actual is not null && allowed.Contains(actual, StringComparer.Ordinal);
        suite.Check(
            $"enum.{checkId}",
            passed,
            passed ? "The enum value is allowed by the runtime model." : "The enum value is not allowed by the runtime model.",
            "structure",
            string.Join(", ", allowed),
            actual ?? "missing");
    }

    private static void VerifyExpressions(WorkflowInstance actual, WorkflowScriptVerificationSuite suite)
    {
        var bindingPassed = string.Equals(actual.ExpressionBinding.Language, ExpressionContract.CurrentLanguage, StringComparison.Ordinal)
            && string.Equals(actual.ExpressionBinding.LanguageVersion, ExpressionContract.CurrentLanguageVersion, StringComparison.Ordinal)
            && string.Equals(actual.ExpressionBinding.CompileFeedbackContract, ExpressionContract.DetailedCompileFeedbackContract, StringComparison.Ordinal);
        suite.Check(
            "expressions.binding",
            bindingPassed,
            bindingPassed ? "The root expression binding is supported." : "The root expression binding is not supported by this runtime.",
            "expression",
            ExpressionContract.CurrentLanguage,
            actual.ExpressionBinding.Language);

        var compiler = new ExpressionCompilerRouter();
        foreach (var transition in actual.GetTransitionNodes().Values)
        {
            VerifyExpression(compiler, actual, transition.Id, "guard", transition.GuardExpression, suite);
            VerifyExpression(compiler, actual, transition.Id, "succeed", transition.SucceedExpression, suite);
        }
    }

    private static void VerifyExpression(
        ExpressionCompilerRouter compiler,
        WorkflowInstance actual,
        string transitionId,
        string expressionName,
        ExpressionDefinition? expression,
        WorkflowScriptVerificationSuite suite)
    {
        if (expression is null)
        {
            suite.Check($"expressions.{transitionId}.{expressionName}", false, "The expression definition is missing.", "expression", "predicate", "missing");
            return;
        }

        try
        {
            var compile = compiler.Compile(actual.ExpressionBinding, expression, $"transition:{transitionId}/{expressionName}Expression");
            suite.Check(
                $"expressions.{transitionId}.{expressionName}",
                compile.IsSuccess,
                compile.IsSuccess ? "The C# expression compiles." : compile.Feedback.Message,
                "expression",
                "valid synchronous C# predicate",
                compile.IsSuccess ? "valid" : compile.Feedback.DiagnosticCode);
        }
        catch (Exception exception)
        {
            suite.Check($"expressions.{transitionId}.{expressionName}", false, exception.Message, "expression", "valid synchronous C# predicate", "exception");
        }
    }

    private static void VerifyGraphReferences(WorkflowInstance actual, WorkflowScriptVerificationSuite suite)
    {
        var states = actual.GetStateNodes();
        var transitions = actual.GetTransitionNodes();
        suite.Check("graph.start_state", states.ContainsKey(actual.StartNodeId), "The start node must identify a state node.", "graph", "state node", actual.StartNodeId);
        suite.Check("graph.current_state", states.ContainsKey(actual.CurrentNodeId), "The current node must identify a state node.", "graph", "state node", actual.CurrentNodeId);
        if (!string.IsNullOrWhiteSpace(actual.EndNodeId))
        {
            suite.Check("graph.end_state", states.ContainsKey(actual.EndNodeId), "The end node must identify a state node.", "graph", "state node", actual.EndNodeId);
        }

        foreach (var pair in actual.Nodes)
        {
            suite.Check(
                $"graph.node_id.{pair.Key}",
                string.Equals(pair.Key, pair.Value.Id, StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(pair.Value.Id),
                "The node dictionary key and node id must agree.",
                "graph",
                pair.Key,
                pair.Value.Id);
        }

        foreach (var state in states.Values)
        {
            foreach (var group in state.Groups)
            {
                foreach (var transitionId in group.TransitionIds)
                {
                    var exists = transitions.ContainsKey(transitionId);
                    suite.Check(
                        $"graph.group.{state.Id}.{group.Id}.{transitionId}",
                        exists,
                        exists ? "The group transition reference exists." : "The group references a missing transition.",
                        "graph",
                        "transition node",
                        transitionId);
                }
            }
        }

        foreach (var transition in transitions.Values)
        {
            if (!string.IsNullOrWhiteSpace(transition.TargetNodeId))
            {
                var exists = states.ContainsKey(transition.TargetNodeId);
                suite.Check(
                    $"graph.target.{transition.Id}",
                    exists,
                    exists ? "The transition target exists and is a state." : "The transition target state is missing.",
                    "graph",
                    "state node",
                    transition.TargetNodeId);
            }
        }
    }

    private static void VerifyProjections(WorkflowInstance actual, WorkflowScriptVerificationSuite suite)
    {
        foreach (var transition in actual.GetTransitionNodes().Values.OfType<CommandTransition>())
        {
            var bindings = GetOutputBindings(transition.Command.Parameters);
            foreach (var binding in bindings)
            {
                var valid = !string.IsNullOrWhiteSpace(binding.Key)
                    && (binding.Value is not string text || !text.StartsWith("$", StringComparison.Ordinal)
                        || string.Equals(text, "$result", StringComparison.Ordinal)
                        || (text.StartsWith(ContextPrefix, StringComparison.Ordinal) && text.Length > ContextPrefix.Length));
                if (binding.Value is string resultReference
                    && string.Equals(resultReference, "$result", StringComparison.Ordinal)
                    && !string.IsNullOrWhiteSpace(transition.OutputPath)
                    && binding.Key.StartsWith(transition.OutputPath + ".", StringComparison.Ordinal))
                {
                    valid = false;
                }

                suite.Check(
                    $"projection.binding.{transition.Id}.{binding.Key}",
                    valid,
                    valid ? "The output binding has a supported projection shape." : "The output binding is invalid or self-referential.",
                    "projection",
                    "literal, $result, or $context:<path>",
                    Convert.ToString(binding.Value, CultureInfo.InvariantCulture));
            }

            if (!IsExternalStep(transition.StepKind))
            {
                continue;
            }

            var parameters = transition.Command.Parameters;
            var resumeOutputKey = GetString(parameters, "resumeOutputKey");
            var projectionMode = GetString(parameters, "projectionMode");
            if (string.IsNullOrWhiteSpace(transition.OutputPath) && string.IsNullOrWhiteSpace(resumeOutputKey))
            {
                continue;
            }

            var modeValid = string.Equals(projectionMode, CanonicalProjection, StringComparison.Ordinal)
                || string.Equals(projectionMode, LegacyNestedProjection, StringComparison.Ordinal);
            suite.Check(
                $"projection.mode.{transition.Id}",
                modeValid,
                modeValid ? "The external projection mode is explicit." : "The external transition must declare canonical or legacyNested projection mode.",
                "projection",
                $"{CanonicalProjection} or {LegacyNestedProjection}",
                projectionMode ?? "missing");

            var canonicalValid = !string.Equals(projectionMode, CanonicalProjection, StringComparison.Ordinal)
                || (!string.IsNullOrWhiteSpace(transition.OutputPath)
                    && !string.IsNullOrWhiteSpace(resumeOutputKey)
                    && GetStrings(parameters, "requiredInputs").Contains(resumeOutputKey, StringComparer.Ordinal));
            suite.Check(
                $"projection.resume_key.{transition.Id}",
                canonicalValid,
                canonicalValid ? "The canonical resume result projection names and validates its payload path." : "The canonical projection needs resumeOutputKey, outputPath, and a matching requiredInputs entry.",
                "projection",
                "resumeOutputKey included in requiredInputs",
                resumeOutputKey ?? "missing");
        }
    }

    private static void VerifyOutputFamilyProducers(WorkflowInstance actual, WorkflowScriptVerificationSuite suite)
    {
        foreach (var transition in actual.GetTransitionNodes().Values)
        {
            var bindings = transition is CommandTransition commandTransition
                ? GetOutputBindings(commandTransition.Command.Parameters)
                : new Dictionary<string, object?>(StringComparer.Ordinal);
            var families = (transition.PublishesOutputFamilies ?? [])
                .Concat(transition.PublishesBlockedOutputFamilies ?? [])
                .Distinct(StringComparer.Ordinal);
            foreach (var family in families)
            {
                var hasProducer = string.Equals(family, transition.OutputPath, StringComparison.Ordinal)
                    || bindings.ContainsKey(family) && HasConcreteBinding(bindings[family]);
                suite.Check(
                    $"dataflow.producer.{transition.Id}.{family}",
                    hasProducer,
                    hasProducer ? "The published output family has a concrete producer on this transition." : "The transition publishes an output family without an outputPath or outputBinding producer.",
                    "dataflow",
                    family,
                    hasProducer ? family : "missing producer");
            }
        }
    }

    private static void VerifyGateValueSemantics(WorkflowInstance actual, WorkflowScriptVerificationSuite suite)
    {
        if (actual.Validation is null)
        {
            suite.Skip("gates.value_semantics", "The workflow has no governed validation contract.", "gate");
            return;
        }

        foreach (var gate in actual.Validation.Gates)
        {
            var expressionValid = gate.Value.PassExpression is not null
                && !string.IsNullOrWhiteSpace(gate.Value.PassExpression.Source);
            suite.Check(
                $"gates.{gate.Key}.pass_expression",
                expressionValid,
                expressionValid ? "The gate has a pass expression." : "The gate is missing a pass expression.",
                "gate",
                "expression",
                expressionValid ? "present" : "missing");
            var families = gate.Value.RequiredOutputFamilies
                .Concat(gate.Value.RequiredMachineReadableOutputFamilies)
                .Concat(gate.Value.RequiredHumanReviewableOutputFamilies)
                .Distinct(StringComparer.Ordinal);
            foreach (var family in families)
            {
                var hasSemantics = gate.Value.ValueSemantics.TryGetValue(family, out var semantics)
                    && !string.IsNullOrWhiteSpace(semantics);
                suite.Check(
                    $"gates.{gate.Key}.value_semantics.{family}",
                    hasSemantics,
                    hasSemantics ? "The gate declares how missing and empty values are interpreted." : "The gate requires an output family but does not declare its value semantics.",
                    "gate",
                    "non-empty value semantics",
                    semantics ?? "missing");
            }
        }
    }

    private static void VerifyReferenceStructure(
        WorkflowInstance actual,
        WorkflowInstance reference,
        Dictionary<string, object?> normalizedDiff,
        WorkflowScriptVerificationSuite suite)
    {
        var actualShape = NormalizeStructure(actual);
        var referenceShape = NormalizeStructure(reference);
        var actualOnly = actualShape.Except(referenceShape, StringComparer.Ordinal).OrderBy(static value => value, StringComparer.Ordinal).ToArray();
        var referenceOnly = referenceShape.Except(actualShape, StringComparer.Ordinal).OrderBy(static value => value, StringComparer.Ordinal).ToArray();
        normalizedDiff["actualOnly"] = actualOnly;
        normalizedDiff["referenceOnly"] = referenceOnly;
        normalizedDiff["actualNodeCount"] = actualShape.Count;
        normalizedDiff["referenceNodeCount"] = referenceShape.Count;
        var passed = actualOnly.Length == 0 && referenceOnly.Length == 0;
        suite.Check(
            "reference.normalized_structure",
            passed,
            passed ? "The candidate and reference share the same normalized graph structure." : "The candidate and reference graph structures differ.",
            "reference",
            "same normalized structure",
            passed ? "same" : $"actualOnly={actualOnly.Length}, referenceOnly={referenceOnly.Length}");
    }

    private static void VerifyRuntimeUpdates(WorkflowInstance actual, bool runtimeEvidenceObserved, WorkflowScriptVerificationSuite suite)
    {
        foreach (var transition in actual.GetTransitionNodes().Values.OfType<CommandTransition>().Where(static transition => transition.StepKind is WorkflowStepKind.StateUpdate or WorkflowStepKind.MemoryWrite))
        {
            var declaredUpdates = GetObjectDictionary(transition.Command.Parameters, "updates");
            if (declaredUpdates.Count == 0)
            {
                continue;
            }
            if (!runtimeEvidenceObserved)
            {
                suite.Skip($"runtime.updates.{transition.Id}", "No run/resume history was supplied, so updates cannot be claimed as observed.", "runtime");
                continue;
            }
            var successfulHistory = actual.History.FirstOrDefault(entry => string.Equals(entry.NodeId, transition.Id, StringComparison.Ordinal) && entry.Status == ExecutionStatus.Succeeded && entry.ContextChanges is not null);
            var historyChanges = successfulHistory?.ContextChanges;
            var historyHasUpdates = historyChanges is not null && declaredUpdates.Keys.All(historyChanges.ContainsKey);
            var contextHasUpdates = declaredUpdates.Keys.All(key => PathValueAccessor.TryGetValue(actual.Context, key, out _));
            suite.Check($"runtime.updates.{transition.Id}", historyHasUpdates && contextHasUpdates, historyHasUpdates && contextHasUpdates ? "The runtime history and context prove the updates were applied." : "The runtime evidence does not prove that every declared update reached context.", "runtime", "successful history ContextChanges and all update paths present", $"history={historyHasUpdates}, context={contextHasUpdates}");
        }
    }
    private static void VerifyRuntimeArtifacts(WorkflowInstance actual, bool runtimeEvidenceObserved, WorkflowScriptVerificationSuite suite)
    {
        foreach (var transition in actual.GetTransitionNodes().Values.OfType<CommandTransition>().Where(static transition => transition.StepKind == WorkflowStepKind.ArtifactEmit))
        {
            if (!runtimeEvidenceObserved)
            {
                suite.Skip($"runtime.artifact.{transition.Id}", "No run/resume history was supplied, so artifact creation cannot be claimed as observed.", "runtime");
                continue;
            }
            var declaredPath = GetString(transition.Command.Parameters, "path");
            var successfulHistory = actual.History.FirstOrDefault(entry => string.Equals(entry.NodeId, transition.Id, StringComparison.Ordinal) && entry.Status == ExecutionStatus.Succeeded && entry.ContextChanges is not null);
            var historyChanges = successfulHistory?.ContextChanges;
            var pathRecorded = !string.IsNullOrWhiteSpace(transition.OutputPath) && historyChanges is not null && historyChanges.TryGetValue(transition.OutputPath, out var recordedPath) && string.Equals(Convert.ToString(recordedPath, CultureInfo.InvariantCulture), declaredPath, StringComparison.Ordinal);
            var exists = !string.IsNullOrWhiteSpace(declaredPath) && File.Exists(Path.GetFullPath(declaredPath));
            var contentMatches = true;
            if (exists && transition.Command.Parameters is not null && transition.Command.Parameters.TryGetValue("content", out var contentValue))
            {
                var expectedContent = Convert.ToString(contentValue, CultureInfo.InvariantCulture) ?? string.Empty;
                contentMatches = string.Equals(File.ReadAllText(Path.GetFullPath(declaredPath!)), expectedContent, StringComparison.Ordinal);
            }
            suite.Check($"runtime.artifact.{transition.Id}", pathRecorded && exists && contentMatches, pathRecorded && exists && contentMatches ? "The runtime history and filesystem prove the artifact was created by this transition." : "The runtime evidence does not prove that this transition created the declared artifact.", "runtime", "successful history path evidence and matching artifact content", $"historyPath={pathRecorded}, exists={exists}, content={contentMatches}");
        }
    }
    private static void VerifyRuntimeProvenance(bool runtimeEvidenceObserved, bool persistedRuntimeEvidencePresent, WorkflowScriptVerificationSuite suite)

    {

        if (!persistedRuntimeEvidencePresent)

        {

            suite.Skip("runtime.provenance", "No persisted runtime-shaped evidence was supplied, so runtime claims remain unobserved.", "runtime");

            return;

        }



        suite.Check(

            "runtime.provenance",

            runtimeEvidenceObserved,

            runtimeEvidenceObserved ? "Runtime evidence carries an in-process provenance marker from the workflow service." : "Persisted history or gate evidence has no in-process provenance marker and cannot be treated as observed runtime behavior.",

            "runtime",

            "runtime-owned provenance marker",

            runtimeEvidenceObserved ? "observed" : "untrusted persisted evidence");

    }



    private static void VerifyRuntimeGateEvidence(WorkflowInstance actual, bool runtimeEvidenceObserved, WorkflowScriptVerificationSuite suite)
    {
        var hasGateContract = actual.Validation?.Gates.Count > 0;
        if (hasGateContract != true)
        {
            suite.Skip("runtime.gate_evidence", "The workflow has no governed gate contract to observe.", "runtime");
            return;
        }

        if (!runtimeEvidenceObserved)
        {
            if (actual.LastGateEvaluation is null)
            {
                suite.Skip("runtime.gate_evidence", "No runtime provenance was supplied, so gate results cannot be claimed as observed.", "runtime");
            }
            else
            {
                suite.Check("runtime.gate_evidence", false, "A persisted gate evaluation has no in-process runtime provenance and cannot be accepted as observed evidence.", "runtime", "runtime-owned provenance marker", "missing");
            }

            return;
        }

        if (actual.LastGateEvaluation is null)
        {
            suite.Skip("runtime.gate_evidence", "A run/resume operation exists but no gate evaluation was persisted.", "runtime");
            return;
        }

        var evaluation = actual.LastGateEvaluation;
        var identityMatches = string.Equals(evaluation.InstanceId, actual.InstanceId, StringComparison.Ordinal);
        var transition = actual.Nodes.TryGetValue(evaluation.TransitionId, out var transitionNode)
            ? transitionNode as TransitionBase
            : null;
        var gateBelongsToTransition = transition is not null
            && !string.IsNullOrWhiteSpace(evaluation.GateId)
            && GetTransitionGateIds(transition).Contains(evaluation.GateId, StringComparer.Ordinal);
        var latestTransitionHistory = actual.History.LastOrDefault(entry => entry.NodeType == TaskNodeType.Transition
            && string.Equals(entry.NodeId, evaluation.TransitionId, StringComparison.Ordinal));
        var transitionSucceeded = latestTransitionHistory?.Status == ExecutionStatus.Succeeded;
        var noMissingFamilies = evaluation.MissingOutputFamilies.Count == 0 && evaluation.EmptyOutputFamilies.Count == 0;
        var passed = evaluation.Passed && identityMatches && gateBelongsToTransition && transitionSucceeded && noMissingFamilies;
        suite.Check("runtime.gate_evidence", passed, passed ? "The runtime gate evaluation passed, belongs to this instance and transition, has complete families, and matches the latest successful transition history entry." : "The runtime gate evidence must pass, belong to this instance and its declared transition gate, have no missing or empty required families, and match the latest successful transition history entry.", "runtime", "passed instance-bound evaluation, declared gate, complete families, and latest successful transition history", $"passed={evaluation.Passed}, identity={identityMatches}, gateBinding={gateBelongsToTransition}, transitionHistory={transitionSucceeded}, missing={evaluation.MissingOutputFamilies.Count}, empty={evaluation.EmptyOutputFamilies.Count}");
    }
    private static void VerifyBlockedAndTerminalEvidence(WorkflowInstance actual, bool runtimeEvidenceObserved, WorkflowScriptVerificationSuite suite)

    {

        if (!runtimeEvidenceObserved)

        {

            suite.Skip("runtime.blocked_route", "No run/resume history or persisted gate evidence was supplied, so blocked-route behavior cannot be claimed as observed.", "runtime");

            suite.Skip("runtime.terminal_route", "No run/resume history or persisted gate evidence was supplied, so terminal behavior cannot be claimed as observed.", "runtime");

            return;

        }



        if (actual.Status == WorkflowStatus.WaitingExternal)

        {

            var waitHistory = actual.History.Any(static entry => entry.Status == ExecutionStatus.Suspended);

            suite.Check("runtime.blocked_route", actual.ActiveWaitGroups.Count > 0 && waitHistory, "The blocked state must have a pending wait group and suspended history.", "runtime", "wait group and suspended history", $"waitGroups={actual.ActiveWaitGroups.Count}, suspendedHistory={waitHistory}");

            return;

        }



        suite.Skip("runtime.blocked_route", "The candidate did not produce a WaitingExternal state.", "runtime");

        if (actual.Status == WorkflowStatus.Succeeded)

        {

            var terminal = !string.IsNullOrWhiteSpace(actual.EndNodeId)

                && string.Equals(actual.CurrentNodeId, actual.EndNodeId, StringComparison.Ordinal);

            suite.Check("runtime.terminal_route", terminal, terminal ? "The successful runtime ended at the declared end state." : "The successful runtime did not end at the declared end state.", "runtime", actual.EndNodeId, actual.CurrentNodeId);

        }

        else

        {

            suite.Skip("runtime.terminal_route", "The candidate did not reach a successful terminal state.", "runtime");

        }

    }

    private static bool HasRuntimeEvidence(WorkflowInstance instance)
        => WorkflowRuntimeEvidenceRegistry.IsObserved(instance);

    private static bool HasPersistedRuntimeEvidence(WorkflowInstance instance)
        => instance.History.Any(static entry => entry.Status is ExecutionStatus.Started or ExecutionStatus.Succeeded or ExecutionStatus.Failed or ExecutionStatus.Suspended)
            || instance.LastGateEvaluation is not null;

    private static IReadOnlyList<string> NormalizeStructure(WorkflowInstance instance)
    {
        var values = new List<string>
        {
            $"root|template={instance.TemplateKind}|start={instance.StartNodeId}|end={instance.EndNodeId}",
        };
        foreach (var node in instance.Nodes.Values.OrderBy(static node => node.Id, StringComparer.Ordinal))
        {
            switch (node)
            {
                case StateNode state:
                    values.Add($"state|{state.Id}|phase={state.WorkflowPhase}|groups={string.Join(',', state.Groups.OrderBy(static group => group.Id, StringComparer.Ordinal).Select(group => group.Id + ':' + string.Join(',', group.TransitionIds.OrderBy(static id => id, StringComparer.Ordinal))))}");
                    break;
                case TransitionBase transition:
                    values.Add($"transition|{GetNodeKind(transition)}|{transition.Id}|step={transition.StepKind}|target={transition.TargetNodeId}|output={transition.OutputPath}|routes={string.Join(',', (transition.TerminalRoutes ?? []).Concat(transition.BlockedRoutes ?? []).OrderBy(static route => route, StringComparer.Ordinal))}");
                    break;
            }
        }

        return values;
    }

    private static string GetNodeKind(ITaskNode node)
        => node switch
        {
            StateNode => "state",
            CommandTransition => "command",
            ExpressionTransition => "expr",
            ToBeRefinedTransition => "tbr",
            _ => string.Empty,
        };

    private static bool IsExternalStep(WorkflowStepKind stepKind)
        => stepKind is WorkflowStepKind.ModelThink or WorkflowStepKind.McpCall or WorkflowStepKind.SubagentCall or WorkflowStepKind.AskUser or WorkflowStepKind.WaitResume;

    private static bool HasConcreteBinding(object? value)
        => value switch
        {
            string text when string.Equals(text, "$result", StringComparison.Ordinal) => true,
            string text when text.StartsWith(ContextPrefix, StringComparison.Ordinal) => text.Length > ContextPrefix.Length,
            string text => !string.IsNullOrWhiteSpace(text),
            null => false,
            _ => true,
        };

    private static IReadOnlyDictionary<string, object?> GetOutputBindings(IReadOnlyDictionary<string, object?>? parameters)
        => GetObjectDictionary(parameters, "outputBindings");

    private static Dictionary<string, object?> GetObjectDictionary(IReadOnlyDictionary<string, object?>? parameters, string key)
    {
        if (parameters?.TryGetValue(key, out var value) != true || value is null)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal);
        }

        if (value is IDictionary<string, object?> mutable)
        {
            return mutable.ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal);
        }

        if (value is IReadOnlyDictionary<string, object?> readOnly)
        {
            return readOnly.ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal);
        }

        if (value is JsonElement { ValueKind: JsonValueKind.Object } element)
        {
            return element.EnumerateObject().ToDictionary(static property => property.Name, static property => (object?)property.Value.Clone(), StringComparer.Ordinal);
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal);
    }

    private static string? GetString(IReadOnlyDictionary<string, object?>? values, string key)
    {
        if (values?.TryGetValue(key, out var value) != true || value is null)
        {
            return null;
        }

        return value switch
        {
            string text => text,
            JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture),
        };
    }

    private static IReadOnlyList<string> GetTransitionGateIds(TransitionBase transition)
    {
        if (transition.SatisfiesGateIds is { Count: > 0 })
        {
            return transition.SatisfiesGateIds.Where(static value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).ToArray();
        }

        return transition is CommandTransition commandTransition
            ? GetStrings(commandTransition.Command.Parameters, "satisfiesGateIds")
            : [];
    }

    private static IReadOnlyList<string> GetStrings(IReadOnlyDictionary<string, object?>? values, string key)
    {
        if (values?.TryGetValue(key, out var value) != true || value is null)
        {
            return [];
        }

        return value switch
        {
            string text when !string.IsNullOrWhiteSpace(text) => [text],
            IEnumerable<string> strings => strings.Where(static item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.Ordinal).ToArray(),
            IEnumerable<object?> objects => objects.Select(item => Convert.ToString(item, CultureInfo.InvariantCulture)).Where(static item => !string.IsNullOrWhiteSpace(item)).Cast<string>().Distinct(StringComparer.Ordinal).ToArray(),
            JsonElement { ValueKind: JsonValueKind.Array } element => element.EnumerateArray().Select(static item => item.ValueKind == JsonValueKind.String ? item.GetString() : item.GetRawText()).Where(static item => !string.IsNullOrWhiteSpace(item)).Cast<string>().Distinct(StringComparer.Ordinal).ToArray(),
            _ => [],
        };
    }

    private static string? GetContextString(IReadOnlyDictionary<string, object?> context, string key)
    {
        return context.TryGetValue(key, out var value)
            ? Convert.ToString(value, CultureInfo.InvariantCulture)
            : null;
    }
}
