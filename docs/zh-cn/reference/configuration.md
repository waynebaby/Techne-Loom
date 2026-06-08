# 配置参考

[English](../../en/reference/configuration.md)

当前公开配置面刻意保持精简，并且主要由 CLI 参数驱动。

## Runtime 输入

- `--workflow-file` 指向 SO 要执行或检查的持久化 workflow file。
- `--context-file` 为 `so run` 注入初始结构化 context 对象。
- `--result-file` 为 `so resume` 注入结构化 resume envelope。
- `--guide --lang ... --section ... --export ...` 控制 guide 的解析与导出行为。

## Sidecar 文件

- workflow file 本身会被回写为最新状态。
- workflow 旁边的 `.events.jsonl` 用作 append-on-growth 事件历史。
- 发布后的 `so --guide` 资产位于 `guide-assets/<lang>/so-guide.md`。

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

`so resume` 的 `--result-file` JSON 示例：

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
- AO runtime 落地后，需要加入官方 MCP/stdio host 配置。
- 当前公开 CLI 契约稳定后，还可以补更多 schema 或 config 产物。
