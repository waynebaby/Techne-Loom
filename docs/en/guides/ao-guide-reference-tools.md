# Loom Agent Execution Orchestrator Guide: Roslyn Tools

[中文](../../zh-cn/guides/ao-guide-reference-tools.md) | [Reference index](ao-guide-reference.md) | [Flow](ao-guide-flow.md) | [Hub](ao-guide.md)

Version: 0.3.282
Build: published package 0.3.282

This chapter defines the C# tools available to AO predicate expressions and workflow scripts. AO remains the owner of its execution decisions; these tools only define what Roslyn-authored code may read or compute.

## Two Roslyn surfaces

| Surface | Used by | Capability declaration | Execution boundary |
| --- | --- | --- | --- |
| Predicate expression | `guardExpression`, `succeedExpression`, and gate expressions | New APIs must be listed in `expressionBinding.requiredExpressionCapabilities` | Synchronous, deterministic, and bounded; there is no separate expression timeout |
| Workflow script | `Build`, `Edit`, and `Verify` in `--workflow-script` | No expression capability list; the script semantic analyzer checks every referenced symbol | Trusted, reviewed C# code with constrained references; the 30-second wait is not a hostile-code sandbox |

Both surfaces use C# 12. The language value is `csharp`, the contract is `loom.expression.csharp`, and the compile feedback contract is `detailedCompileFeedbackV1`. Existing expressions that use only context access need no new declaration.

## Predicate expression baseline

The generated expression method receives a statically typed `ExpressionRuntimeContext` named `context`.

| API | Exact shape | Declaration |
| --- | --- | --- |
| Typed context read | `context.Get<T>(string path)` | Baseline; no declaration |
| Context existence | `context.Has(string path)` | Baseline; no declaration |
| Context indexer | `context[string path]` | Baseline; no declaration |
| C# operators | Boolean, comparison, arithmetic, null-coalescing, and conditional operators | Baseline language constructs |

Context paths are limited to six segments. Collection reads are materialized only when the target is an array or `IReadOnlyList<T>`, `IReadOnlyCollection<T>`, or `IEnumerable<T>` of primitive or string values. The runtime limit is 32 items and 32 KiB of projected data.

## Expression capabilities

Declare each capability used by a predicate in `requiredExpressionCapabilities`. The compiler reports the capabilities it actually resolved in compile feedback.

### Ordinal strings

Capability: `loom.expression.string.ordinal`

Accepted methods:

- `string.IsNullOrEmpty(string)`
- `string.IsNullOrWhiteSpace(string)`
- `string.Equals(string, StringComparison)`
- `string.Equals(string, string, StringComparison)`
- `string.Contains(string, StringComparison)`
- `string.StartsWith(string, StringComparison)`
- `string.EndsWith(string, StringComparison)`
- `string.IndexOf(string, StringComparison)`
- `string.IndexOf(string, int, StringComparison)`
- `string.LastIndexOf(string, StringComparison)`
- `string.LastIndexOf(string, int, StringComparison)`

For comparison overloads, the argument must be the compile-time value `StringComparison.Ordinal` or `StringComparison.OrdinalIgnoreCase`. Default and current-culture overloads are rejected.

```csharp
string.Equals(
    context.Get<string>("run.status"),
    "completed",
    StringComparison.OrdinalIgnoreCase)
```

### Math

Capability: `loom.expression.math`

Only these exact overload families are exposed:

- `Math.Abs(decimal|double|short|int|long|sbyte|float)`
- `Math.Min` and `Math.Max` for `byte|decimal|double|short|int|long|sbyte|float|ushort|uint|ulong`
- `Math.Clamp` for `byte|decimal|double|short|int|long|sbyte|float|ushort|uint|ulong`
- `Math.Floor(decimal|double)`, `Math.Ceiling(decimal|double)`, and `Math.Truncate(decimal|double)`
- `Math.Sign(decimal|double|short|int|long|sbyte|float)`
- `Math.Round(decimal|double)`, with the accepted `digits` and `MidpointRounding` overloads

There is no blanket approval for the `Math` type.

```csharp
Math.Clamp(context.Get<int>("score"), 0, 100) >= 60
```

### TimeSpan factories

Capability: `loom.expression.timespan`

Accepted factories are `TimeSpan.FromMilliseconds(double)`, `TimeSpan.FromSeconds(double)`, and `TimeSpan.FromSeconds(long)`. The argument must be a compile-time numeric constant. This capability exists primarily to construct Regex timeouts.

### Native Regex

Capability: `loom.expression.regex`

Matching uses native static C# APIs. Accepted matching overloads are:

- `Regex.IsMatch(string input, string pattern, RegexOptions options, TimeSpan matchTimeout)`
- `Regex.Match(string input, string pattern, RegexOptions options, TimeSpan matchTimeout)`
- `Regex.Matches(string input, string pattern, RegexOptions options, TimeSpan matchTimeout)`
- `Regex.Count(string input, string pattern, RegexOptions options, TimeSpan matchTimeout)`
- `Regex.Replace(string input, string pattern, string replacement, RegexOptions options, TimeSpan matchTimeout)`
- `Regex.Split(string input, string pattern, RegexOptions options, TimeSpan matchTimeout)`
- `Regex.Escape(string)` and `Regex.Unescape(string)` as non-matching helpers

Every matching call must pass a finite, positive timeout no greater than five seconds. The timeout must be created by an approved `TimeSpan.FromSeconds` or `TimeSpan.FromMilliseconds` call with a compile-time number. Allowed options are `None`, `CultureInvariant`, `IgnoreCase`, `Multiline`, `Singleline`, `ExplicitCapture`, `IgnorePatternWhitespace`, and `NonBacktracking`, including bitwise combinations. `Compiled`, `RightToLeft`, and `ECMAScript` are rejected.

The read-only result members `Group.Success`, `Capture.Value`, and `MatchCollection.Count` are available. Regex instances, `Regex.InfiniteMatchTimeout`, cache settings, match-evaluator delegates, and timeout-free overloads are rejected.

`[regex]::Match(...)` is PowerShell syntax, not C#. AO expressions must use the C# form:

```powershell
[regex]::Match($value, 'a+')       # PowerShell; rejected by the C# compiler
```

```csharp
Regex.Match(
    context.Get<string>("value"),
    "a+",
    RegexOptions.CultureInvariant,
    TimeSpan.FromSeconds(1)).Success
```

Malformed patterns and `RegexMatchTimeoutException` remain runtime failures and are not converted into a successful predicate result.

### Invariant parsing

Capability: `loom.expression.parsing.invariant`

Numeric parsing accepts the four-argument `TryParse(string, NumberStyles, IFormatProvider, out T)` shape for `byte`, `sbyte`, `short`, `int`, `long`, `ushort`, `uint`, `ulong`, `float`, `double`, and `decimal`. The provider must be `CultureInfo.InvariantCulture`. Accepted named number styles are `None`, `Integer`, `Number`, `Float`, `HexNumber`, and `Any`.

Additional exact shapes are:

- `Guid.TryParse(string, out Guid)`
- `Guid.TryParse(string, IFormatProvider, out Guid)` with invariant culture
- `DateTimeOffset.TryParseExact(string, string format, IFormatProvider, DateTimeStyles, out DateTimeOffset)`
- `TimeSpan.TryParseExact(string, string format, IFormatProvider, TimeSpanStyles, out TimeSpan)`

Date styles are limited to `None`, `AdjustToUniversal`, `AssumeUniversal`, `AllowWhiteSpaces`, `NoCurrentDateDefault`, and `RoundtripKind`. Duration styles are `None` and `AssumeNegative`.

```csharp
int.TryParse(
    context.Get<string>("attempts"),
    NumberStyles.Integer,
    CultureInfo.InvariantCulture,
    out var attempts) && attempts > 0
```

Culture-dependent `Parse` and `TryParse` shortcuts, implicit current culture, non-constant formats, and unapproved style values are rejected.

### Bounded collections

Capability: `loom.expression.collections.bounded`

The only LINQ methods are:

- `Enumerable.Any<T>(IEnumerable<T>)`
- `Enumerable.Any<T>(IEnumerable<T>, Func<T, bool>)`
- `Enumerable.All<T>(IEnumerable<T>, Func<T, bool>)`
- `Enumerable.Contains<T>(IEnumerable<T>, T)`
- `Enumerable.Count<T>(IEnumerable<T>)`
- `Enumerable.Count<T>(IEnumerable<T>, Func<T, bool>)`
- `Enumerable.SequenceEqual<T>(IEnumerable<T>, IEnumerable<T>)`

The source must be a direct bounded `context.Get<T>(...)` collection. `Any` and `All` lambdas are allowed only at that call site and are checked recursively by the same expression policy. Sorting, projection, grouping, flattening, `Queryable`, custom enumerators, and unbounded materialization are not available.

```csharp
context.Get<IReadOnlyList<int>>("scores").Any(score => score >= 60)
```

### Script enumerable members

Capability: `loom.script.enumerable.members`

Scripts may use only the synchronous Enumerable members explicitly listed in the catalog; sorting, grouping, projection, and unbounded materialization remain unavailable.

## Workflow script tools

Scripts receive the model objects documented by the `Build`, `Edit`, and `Verify` contracts. The host adds these usings automatically:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using Techne.Loom.Abstractions.TaskTracking.Model;
```

Existing script baseline references are `System.Private.CoreLib`, `System.Runtime`, `System.Collections`, `System.Linq`, `System.ObjectModel`, `System.Runtime.Extensions`, and the Loom model assembly. Generic in-memory collections, `Enumerable`, synchronous model reads/writes, and pure computation remain available under the semantic analyzer.

### Script Regex, JSON, and parsing

Script Regex uses the same static overloads, five-second ceiling, and options allowlist as expressions. It does not require `requiredExpressionCapabilities`.

The read-only JSON capability is `loom.script.json.read-only`. Accepted members are `JsonElement.ValueKind`, `JsonElement[int]`, `TryGetProperty(string, out JsonElement)`, `GetProperty(string)`, `GetArrayLength()`, `GetPropertyCount()`, scalar `Get*`/`TryGet*` methods for string, Boolean, `int`, `long`, `decimal`, `double`, `Guid`, `DateTime`, and `DateTimeOffset`, `EnumerateArray()`, `EnumerateObject()`, `ValueEquals(string)`, and `JsonElement.DeepEquals(JsonElement, JsonElement)`.

JSON parsing, serialization, `WriteTo`, mutable DOM APIs, `Clone`, `GetRawText`, and unbounded raw byte/text extraction are not available.

Script invariant parsing uses the same exact methods, provider, format, and style rules as expressions. A script may use locals and `out` variables; it still must pass invariant culture and approved styles explicitly.

```csharp
public static WorkflowInstance Build(WorkflowScriptInput input)
{
    var payload = (JsonElement)input.Context["payload"]!;
    var status = payload.GetProperty("status").GetString() ?? string.Empty;
    return new WorkflowInstance
    {
        RuntimeBinding = input.RuntimeBinding,
        RuntimeVersion = status,
    };
}
```

### Script hashing and encoding

The script-only capabilities are `loom.script.hashing.sha256`, `loom.script.encoding.utf8`, and `loom.script.encoding.hex-base64`.

Accepted exact shapes are:

- `Encoding.UTF8`
- `Encoding.GetBytes(string)` and `Encoding.GetString(byte[])` on the UTF-8 instance
- `SHA256.HashData(byte[])`
- `Convert.ToHexString(byte[])` and `Convert.FromHexString(string)`
- `Convert.ToBase64String(byte[])` and `Convert.FromBase64String(string)`

These operations are in-memory only. File or stream hashing, MD5, SHA-1, algorithm factories, keyed cryptography, and cryptographic randomness are not available.

```csharp
var bytes = Encoding.UTF8.GetBytes(input.RuntimeVersion ?? string.Empty);
var digest = SHA256.HashData(bytes);
var hex = Convert.ToHexString(digest);
var base64 = Convert.ToBase64String(Convert.FromHexString(hex));
```

The script wait timeout does not terminate running in-process code and is not a security sandbox. Scripts are trusted reviewed code with constrained references, not hostile code.

## Rejected families and alternatives

The analyzers reject file and directory I/O, network access, processes, reflection and loading, native interop, environment access, mutable static state, current clocks, randomness, threading, async/await, dynamic dispatch, and code generation. Regex compilation and cache mutation are also unavailable.

Precompute I/O, network, time, or random results outside the expression and write the bounded result into workflow context. Use an authored runtime command or a workflow script contract for file/process/network work. Keep predicates pure and use explicit timeout, culture, comparison, and size arguments.

Compile failures report stable diagnostic codes, source spans, the resolved symbols, and an actionable suggested fix. A successful compile reports only capabilities actually resolved by Roslyn.

[Back to AO Reference index](ao-guide-reference.md) | [中文版本](../../zh-cn/guides/ao-guide-reference-tools.md)

## Catalog identifiers

The shared typed catalog gives every exposed surface a stable capability ID and documentation key. The same IDs are used by AO and SO.

| Capability ID | Documentation key | Surface |
| --- | --- | --- |
| `loom.expression.context.get` | `tools.expression.context.get` | expression |
| `loom.expression.context.has` | `tools.expression.context.has` | expression |
| `loom.expression.context.indexer` | `tools.expression.context.indexer` | expression |
| `loom.expression.string.ordinal` | `tools.expression.string.ordinal` | expression |
| `loom.expression.math` | `tools.expression.math` | expression |
| `loom.expression.timespan` | `tools.expression.timespan` | expression |
| `loom.expression.regex` | `tools.expression.regex` | expression |
| `loom.expression.parsing.invariant` | `tools.expression.parsing.invariant` | expression |
| `loom.expression.collections.bounded` | `tools.expression.collections.bounded` | expression |
| `loom.script.system.core` | `tools.script.system.core` | script |
| `loom.script.system.runtime` | `tools.script.system.runtime` | script |
| `loom.script.system.collections` | `tools.script.collections` | script |
| `loom.script.collections.members` | `tools.script.collections.members` | script |
| `loom.script.system.linq` | `tools.script.enumerable` | script |
| `loom.script.enumerable.members` | `tools.script.enumerable.members` | script |
| `loom.script.system.object-model` | `tools.script.object-model` | script |
| `loom.script.system.runtime-extensions` | `tools.script.runtime-extensions` | script |
| `loom.script.loom-model` | `tools.script.workflow-model` | script |
| `loom.script.model-members` | `tools.script.workflow-model.members` | script |
| `loom.script.regex` | `tools.script.regex` | script |
| `loom.script.json.read-only` | `tools.script.json.read-only` | script |
| `loom.script.parsing.invariant` | `tools.script.parsing.invariant` | script |
| `loom.script.hashing.sha256` | `tools.script.hashing.sha256` | script |
| `loom.script.encoding.utf8` | `tools.script.encoding.utf8` | script |
| `loom.script.encoding.hex-base64` | `tools.script.encoding.hex-base64` | script |
