# 退出码与错误模型

[English](../../en/reference/exit-codes.md) | [根目录](../README.md)

公开二进制应使用紧凑的退出码模型。

## 方向

- `0`：成功完成
- `1`：usage error，例如缺少必需 CLI 参数
- `2`：校验失败或运行时失败
- `3`：带显式 boundary 字段的 blocked payload，或仍需外部处理的 no-progress 阻塞

更丰富的机器可读细节应放在 JSON payload、诊断文件或事件日志里，而不是扩张成庞大的数字退出码分类。
