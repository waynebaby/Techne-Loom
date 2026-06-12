# Exit Codes And Error Model

[中文](../../zh-cn/reference/exit-codes.md) | [Root](../README.md)

The public binaries should use a compact exit-code model.

## Direction

- `0`: completed successfully
- `1`: usage error such as missing required CLI arguments
- `2`: validation failure or runtime failure
- `3`: blocked payload with explicit boundary fields, or a no-progress block, that still requires external resolution

The richer machine-readable detail belongs in JSON payloads, diagnostics files, or event logs rather than in a large numeric exit-code taxonomy.
