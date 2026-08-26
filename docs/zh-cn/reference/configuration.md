# 配置参考

[English](../../en/reference/configuration.md) | [根目录](../README.md)

当前公开配置面刻意保持精简，并且主要由 CLI 参数驱动。

## Runtime 输入

- `--workflow-file` 指向 SO 要执行或检查的持久化 workflow file。
- `--context-file` 为 `dotnet so.dll run` 注入初始结构化 context 对象。
- `--result-file` 为 `dotnet so.dll resume` 注入结构化 resume envelope。
- 不带参数的 `--guide` 会读取与可执行文件放在同一个 runtime package 中的版本匹配英文文档，并返回包含 `version`、`docs_root` 与 `guide_path` 的 JSON；它拒绝 `--lang`、`--section` 和 `--export`

## Sidecar 文件

- workflow file 本身会被回写为最新状态。
- workflow 旁边的 `.events.jsonl` 用作 append-on-growth 事件历史。
- `.events.jsonl.meta.json` 保存 workflow `instance_id`；metadata 缺失或不匹配时，现有 sidecar lineage 无效，追加前必须先重写。
- 发布后的 `dotnet so.dll --guide` 文档会安装到 `<binary>/docs/<package-version>/`；如果二进制目录不可写，则安装到 `%TEMP%/docs/<package-version>/`

## 实际示例

`--context-file` JSON 示例：

```json
{
  "review": {
    "approved": true,
    "summary": "ready to ship"
  },
  "notes": ["carry this into memory extraction"]
}
```

`dotnet so.dll resume` 的 `--result-file` JSON 示例：

```json
{
  "transition_id": "transition.ask",
  "correlation_key": null,
  "payload": {
    "review": {
      "approved": true
    }
  }
}
```

## 当前 Runtime 默认行为

- 当前已 review 的公开 SO 切片在当前进程内使用 in-memory instance store。
- 来自 workflow/context/result 文件的嵌套 JSON 会在求值前规范化成 runtime dictionary/list。
- 目前还没有大型中心化配置文件。

## 计划中的扩展点

- 后续可以把 file-backed 或其他 instance store 暴露为公开配置。
- AO 在本项目里使用文档化的 CLI / package 契约；不需要 MCP host 配置。
- 当前公开 CLI 契约稳定后，还可以补更多 schema 或 config 产物。
