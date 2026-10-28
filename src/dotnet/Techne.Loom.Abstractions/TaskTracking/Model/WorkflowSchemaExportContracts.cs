using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public sealed record WorkflowSchemaDemoExportResult(

    string OutputDirectory,

    string RuntimeBinding,

    string? RuntimeVersion,

    string SchemaFile,

    string DemoFile,
    string ContextContractFile,

    string ModelFile,

    string BuilderScriptFile,

    string VerifierScriptFile,

    IReadOnlyDictionary<string, string> Sha256);

public sealed record WorkflowSchemaContract(
    string SchemaId,
    string SchemaVersion,
    string RuntimeType,
    string PropertyNamingPolicy,
    string NodeStorage,
    string NodeKindDiscriminator,
    IReadOnlyList<string> RootFields,
    IReadOnlyList<string> RequiredRootFields,
    IReadOnlyDictionary<string, IReadOnlyList<string>> NodeFields,
    IReadOnlyDictionary<string, IReadOnlyList<string>> RequiredNodeFields,
    IReadOnlyList<string> TransitionGroupFields,
    IReadOnlyList<string> RequiredTransitionGroupFields,
    IReadOnlyList<string> ExpressionDefinitionFields,
    string ExpressionStringInputCompatibility,
    IReadOnlyDictionary<string, IReadOnlyList<string>> AllowedValues,
    IReadOnlyDictionary<string, string> CommandParameterContracts,
    IReadOnlyList<string> CompileRules);
