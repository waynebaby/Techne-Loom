# Rust+CEL Runtime 路线

[English](../../en/architecture/rust-cel-runtime.md) | [架构索引](README.md)

## 状态

Rust+CEL 是未来第四条 Loom runtime 路线。当前 .NET runtime 尚未实现它；它也不是在 workflow 表达式中执行 Rust 源代码。当前已支持的路线仍然是由 Roslyn 编译的 C#。

## 目的

未来路线是用 Rust 实现跨平台 Loom Runtime Core，并以 CEL 作为规范表达式语言。它面向无法直接承载 .NET runtime 的环境，同时保持同一 workflow wire contract。

## 复用规范合同

该路线必须复用现有 root 字段，不得发明平行 schema：

- `runtimeBinding` 标识负责 workflow 执行的 runtime 与 CLI。
- `expressionBinding` 标识 `language`、`languageVersion`、`contractId`、`contractVersion`、`requiredExpressionCapabilities` 与 `compileFeedbackContract`。
- `ExpressionDefinition` 承载 `kind`、`source`、`entryPoint` 与 `resultType`。
- `ExpressionCompileFeedback` 对成功和失败都遵循 `detailedCompileFeedbackV1`。

Rust runtime 必须拒绝不支持的 binding、异步执行、不支持的表达式形态与缺失的合同 capability。它必须输出稳定 diagnostic code、category、source span、可行动 message、suggested fix、referenced symbols、compiler identity、解析后的 form、result type、capabilities 与 warnings。不能只把宿主解释器异常原样透传。当前信任模型仍是受信任模板，不是恶意代码 sandbox。

## CEL 编译分层

1. 校验 JSON binding 与 root runtime ownership。
2. 校验 expression definition 形状与 result type。
3. 解析 CEL syntax，并将 source span 映射到原始表达式。
4. 根据只读 workflow contract API 解析 symbol。
5. 对受信任模板执行模型实施 capability 与 resource limits。
6. 按与 .NET 路线相同的 contract identity 与 diagnostic categories 输出 `ExpressionCompileFeedback`。

跨语言迁移仍由 skill 负责。将 C# 翻译为 CEL 的 skill 必须保留原 source、translated source、translator/tool identity、review evidence 与 compile feedback。runtime 永不自动翻译 source。

## 平台与发布矩阵

| 目标 | Runtime artifact | 分发方式 | 验证 |
| --- | --- | --- | --- |
| Windows x64 | `loom-runtime.exe` | GitHub Releases installer 与 checksum | CI compile、单元测试、smoke workflow |
| Linux x64 | `loom-runtime` | GitHub Releases archive 与 checksum | CI compile、单元测试、smoke workflow |
| macOS arm64 | `loom-runtime` | GitHub Releases archive 与 checksum | CI compile、单元测试、smoke workflow |
| macOS x64 | `loom-runtime` | GitHub Releases archive 与 checksum | CI compile、单元测试、smoke workflow |

Installer 与 archive 必须发布 SHA-256 checksum。发布自动化必须保持 runtime version、CLI contract version 与内置 guide bundle 一致。

## 六个里程碑

1. **文档先行**：在双语 architecture 文档中冻结术语、规范字段、信任模型、feedback contract 与平台目标。
2. **原型验证**：验证 CEL parsing、只读 context 访问、source span、capability checks 与代表性 workflow predicate。
3. **合同冻结**：发布 binding、definition、feedback、error 与跨 runtime fixture 的兼容性测试。
4. **Runtime 实现**：实现 Rust core、resource limits、workflow loading、compile cache 与 execution lifecycle。
5. **CLI 发布**：发布平台 artifact、checksum、startup contract、`--guide`、compile、run、resume 与 audit surface。
6. **.NET adapter 集成**：在同一 router 后接入 adapter，不改变 canonical wire schema，也不削弱 C# 行为。

在全部里程碑与共享 feedback contract 完成前，Rust+CEL 必须标记为 future-only，不得标记为已支持的表达式语言。
