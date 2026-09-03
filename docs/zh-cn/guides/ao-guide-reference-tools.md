# Loom Agent Execution Orchestrator Guide：Roslyn 工具

[English](../../en/guides/ao-guide-reference-tools.md) | [参考索引](ao-guide-reference.md) | [流程](ao-guide-flow.md) | [入口](ao-guide.md)

版本：0.3.282
构建：已发布的 0.3.282 包

本章定义 AO 的 predicate expression 和 workflow script 可以使用的 C# 工具。AO 仍然负责自己的执行决策；这些工具只规定 Roslyn 代码可以读取和计算什么。

## 两种 Roslyn 表面

| 表面 | 使用位置 | 能力声明 | 执行边界 |
| --- | --- | --- | --- |
| Predicate expression | `guardExpression`、`succeedExpression` 和 gate 表达式 | 新 API 必须写入 `expressionBinding.requiredExpressionCapabilities` | 同步、确定性且有边界；没有单独的 expression 超时 |
| Workflow script | `--workflow-script` 的 `Build`、`Edit` 和 `Verify` | 没有 expression 能力列表；脚本语义分析器检查每个引用的符号 | 受信任、经过审查的 C# 代码，使用受限引用；30 秒等待不是恶意代码沙箱 |

两种表面都使用 C# 12。语言值是 `csharp`，契约是 `loom.expression.csharp`，编译反馈契约是 `detailedCompileFeedbackV1`。只使用 context 访问的旧表达式不需要新增声明。

## Expression 基线

生成的表达式方法收到一个名为 `context`、类型为 `ExpressionRuntimeContext` 的参数。

| API | 精确形状 | 声明 |
| --- | --- | --- |
| 类型化 context 读取 | `context.Get<T>(string path)` | 基线，不需要声明 |
| 判断 context 路径 | `context.Has(string path)` | 基线，不需要声明 |
| context 索引器 | `context[string path]` | 基线，不需要声明 |
| C# 运算符 | 布尔、比较、算术、空合并和条件运算符 | 基础语言结构 |

context 路径最多六段。只有目标类型为数组、`IReadOnlyList<T>`、`IReadOnlyCollection<T>` 或 `IEnumerable<T>`，且元素是原始类型或字符串时，集合读取才会被物化。运行时限制为最多 32 项和 32 KiB 投影数据。

## Expression 能力

在 `requiredExpressionCapabilities` 中声明 predicate 使用的每项能力。编译反馈会报告 Roslyn 实际解析到的能力。

### 序号字符串

能力：`loom.expression.string.ordinal`

允许的方法：

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

比较重载的参数必须是编译期值 `StringComparison.Ordinal` 或 `StringComparison.OrdinalIgnoreCase`。默认比较和当前文化比较重载会被拒绝。

```csharp
string.Equals(
    context.Get<string>("run.status"),
    "completed",
    StringComparison.OrdinalIgnoreCase)
```

### Math

能力：`loom.expression.math`

只开放以下精确重载族：

- `Math.Abs(decimal|double|short|int|long|sbyte|float)`
- `Math.Min` 和 `Math.Max`，参数类型为 `byte|decimal|double|short|int|long|sbyte|float|ushort|uint|ulong`
- `Math.Clamp`，参数类型为 `byte|decimal|double|short|int|long|sbyte|float|ushort|uint|ulong`
- `Math.Floor(decimal|double)`、`Math.Ceiling(decimal|double)` 和 `Math.Truncate(decimal|double)`
- `Math.Sign(decimal|double|short|int|long|sbyte|float)`
- `Math.Round(decimal|double)`，以及允许的 `digits` 和 `MidpointRounding` 重载

不会整体放行 `Math` 类型。

```csharp
Math.Clamp(context.Get<int>("score"), 0, 100) >= 60
```

### TimeSpan 工厂

能力：`loom.expression.timespan`

允许 `TimeSpan.FromMilliseconds(double)`、`TimeSpan.FromSeconds(double)` 和 `TimeSpan.FromSeconds(long)`。参数必须是编译期数字常量。这项能力主要用于构造 Regex 超时。

### 原生 Regex

能力：`loom.expression.regex`

使用原生 C# 静态 API。允许的匹配重载是：

- `Regex.IsMatch(string input, string pattern, RegexOptions options, TimeSpan matchTimeout)`
- `Regex.Match(string input, string pattern, RegexOptions options, TimeSpan matchTimeout)`
- `Regex.Matches(string input, string pattern, RegexOptions options, TimeSpan matchTimeout)`
- `Regex.Count(string input, string pattern, RegexOptions options, TimeSpan matchTimeout)`
- `Regex.Replace(string input, string pattern, string replacement, RegexOptions options, TimeSpan matchTimeout)`
- `Regex.Split(string input, string pattern, RegexOptions options, TimeSpan matchTimeout)`
- `Regex.Escape(string)` 和 `Regex.Unescape(string)`，作为不执行匹配的辅助方法

每次匹配都必须传入有限、为正且不超过 5 秒的超时。超时必须由带编译期数字的 `TimeSpan.FromSeconds` 或 `TimeSpan.FromMilliseconds` 调用创建。允许的 options 是 `None`、`CultureInvariant`、`IgnoreCase`、`Multiline`、`Singleline`、`ExplicitCapture`、`IgnorePatternWhitespace` 和 `NonBacktracking`，可以按位组合。`Compiled`、`RightToLeft` 和 `ECMAScript` 会被拒绝。

允许读取 `Group.Success`、`Capture.Value` 和 `MatchCollection.Count`。Regex 实例、`Regex.InfiniteMatchTimeout`、缓存设置、match-evaluator 委托和无超时重载都会被拒绝。

`[regex]::Match(...)` 是 PowerShell 语法，不是 C#。AO expression 必须使用 C# 形式：

```powershell
[regex]::Match($value, 'a+')       # PowerShell；C# 编译器会拒绝
```

```csharp
Regex.Match(
    context.Get<string>("value"),
    "a+",
    RegexOptions.CultureInvariant,
    TimeSpan.FromSeconds(1)).Success
```

图样错误和 `RegexMatchTimeoutException` 仍然是运行时失败，不会被转换成成功的 predicate。

### Invariant parsing

能力：`loom.expression.parsing.invariant`

数值解析使用四参数形状 `TryParse(string, NumberStyles, IFormatProvider, out T)`，支持 `byte`、`sbyte`、`short`、`int`、`long`、`ushort`、`uint`、`ulong`、`float`、`double` 和 `decimal`。provider 必须是 `CultureInfo.InvariantCulture`。允许的命名 NumberStyles 是 `None`、`Integer`、`Number`、`Float`、`HexNumber` 和 `Any`。

其他精确形状：

- `Guid.TryParse(string, out Guid)`
- `Guid.TryParse(string, IFormatProvider, out Guid)`，并使用 invariant culture
- `DateTimeOffset.TryParseExact(string, string format, IFormatProvider, DateTimeStyles, out DateTimeOffset)`
- `TimeSpan.TryParseExact(string, string format, IFormatProvider, TimeSpanStyles, out TimeSpan)`

DateTimeStyles 只允许 `None`、`AdjustToUniversal`、`AssumeUniversal`、`AllowWhiteSpaces`、`NoCurrentDateDefault` 和 `RoundtripKind`。TimeSpanStyles 允许 `None` 和 `AssumeNegative`。

```csharp
int.TryParse(
    context.Get<string>("attempts"),
    NumberStyles.Integer,
    CultureInfo.InvariantCulture,
    out var attempts) && attempts > 0
```

依赖文化的 `Parse` 和 `TryParse` 快捷重载、隐式当前文化、非编译期 format 和未批准的 style 会被拒绝。

### 有界集合

能力：`loom.expression.collections.bounded`

唯一允许的 LINQ 方法是：

- `Enumerable.Any<T>(IEnumerable<T>)`
- `Enumerable.Any<T>(IEnumerable<T>, Func<T, bool>)`
- `Enumerable.All<T>(IEnumerable<T>, Func<T, bool>)`
- `Enumerable.Contains<T>(IEnumerable<T>, T)`
- `Enumerable.Count<T>(IEnumerable<T>)`
- `Enumerable.Count<T>(IEnumerable<T>, Func<T, bool>)`
- `Enumerable.SequenceEqual<T>(IEnumerable<T>, IEnumerable<T>)`

来源必须是直接的、有边界的 `context.Get<T>(...)` 集合。`Any` 和 `All` 的 lambda 只能出现在这个调用位置，并且会再次经过相同的 expression 策略检查。排序、投影、分组、展平、`Queryable`、自定义 enumerator 和无边界物化都不可用。

```csharp
context.Get<IReadOnlyList<int>>("scores").Any(score => score >= 60)
```

### Script enumerable members

Capability: `loom.script.enumerable.members`

脚本只能使用 catalog 中明确列出的同步 Enumerable 成员；不允许排序、分组、投影或无界物化。

## Workflow script 工具

脚本收到 `Build`、`Edit` 和 `Verify` 契约中定义的模型对象。宿主会自动加入以下 using：

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

现有脚本基线引用包括 `System.Private.CoreLib`、`System.Runtime`、`System.Collections`、`System.Linq`、`System.ObjectModel`、`System.Runtime.Extensions` 和 Loom model assembly。泛型内存集合、`Enumerable`、同步模型读写和纯计算仍可在语义分析器约束下使用。

### Script Regex、JSON 和解析

Script Regex 使用与 expression 相同的静态重载、5 秒上限和 options 白名单，不需要 `requiredExpressionCapabilities`。

只读 JSON 能力是 `loom.script.json.read-only`。允许 `JsonElement.ValueKind`、`JsonElement[int]`、`TryGetProperty(string, out JsonElement)`、`GetProperty(string)`、`GetArrayLength()`、`GetPropertyCount()`，用于 string、Boolean、`int`、`long`、`decimal`、`double`、`Guid`、`DateTime` 和 `DateTimeOffset` 的标量 `Get*`/`TryGet*` 方法，`EnumerateArray()`、`EnumerateObject()`、`ValueEquals(string)` 以及 `JsonElement.DeepEquals(JsonElement, JsonElement)`。

JSON 解析、序列化、`WriteTo`、可变 DOM、`Clone`、`GetRawText` 和无界原始字节/文本提取都不可用。

Script invariant parsing 使用与 expression 相同的精确方法、provider、format 和 style 规则。脚本可以使用局部变量和 `out` 变量，但仍必须显式传入 invariant culture 和批准的 styles。

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

### Script hashing 和 encoding

只在脚本开放的能力是 `loom.script.hashing.sha256`、`loom.script.encoding.utf8` 和 `loom.script.encoding.hex-base64`。

允许的精确形状是：

- `Encoding.UTF8`
- UTF-8 实例上的 `Encoding.GetBytes(string)` 和 `Encoding.GetString(byte[])`
- `SHA256.HashData(byte[])`
- `Convert.ToHexString(byte[])` 和 `Convert.FromHexString(string)`
- `Convert.ToBase64String(byte[])` 和 `Convert.FromBase64String(string)`

这些操作只处理内存数据。文件或 stream hashing、MD5、SHA-1、算法工厂、带密钥密码操作和密码学随机数都不可用。

```csharp
var bytes = Encoding.UTF8.GetBytes(input.RuntimeVersion ?? string.Empty);
var digest = SHA256.HashData(bytes);
var hex = Convert.ToHexString(digest);
var base64 = Convert.ToBase64String(Convert.FromHexString(hex));
```

脚本等待超时不会终止进程内正在运行的代码，也不是安全沙箱。脚本是受信任、经过审查且引用受限的代码，不是恶意代码。

## 拒绝的能力族和替代方式

分析器拒绝文件和目录 I/O、网络、进程、反射和加载、native interop、环境访问、可变静态状态、当前时钟、随机数、threading、async/await、dynamic dispatch 和代码生成。Regex 编译与缓存修改也不可用。

把 I/O、网络、时间或随机结果预先在 expression 外计算，再把有边界的结果写入 workflow context。文件、进程和网络工作应使用已声明的 runtime command 或 workflow script 契约。predicate 保持纯计算，并显式传入 timeout、culture、comparison 和 size 参数。

编译失败会报告稳定诊断码、源代码位置、解析到的符号和可执行的修复建议。编译成功只报告 Roslyn 实际解析到的能力。

[返回 AO 参考索引](ao-guide-reference.md) | [English version](../../en/guides/ao-guide-reference-tools.md)

## Catalog 标识

共享 typed catalog 为每个开放表面提供稳定的 capability ID 和文档键。AO 和 SO 使用同一组 ID。

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
