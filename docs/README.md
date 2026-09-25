# Techne Loom Docs

- [English](en/README.md)
- [中文](zh-cn/README.md)

Techne Loom keeps the full public documentation under `/docs`.

Use these entry points:

- [English docs](en/README.md) for the English documentation tree.
- [中文文档](zh-cn/README.md) for the Chinese documentation tree.

If another agent needs to continue implementation, start with the architecture roadmap:

- [English roadmap](en/architecture/implementation-roadmap.md)
- [中文路线图](zh-cn/architecture/implementation-roadmap.md)

The two trees stay mirrored by path. Shared code, JSON, transcripts, and diagrams may be reused across languages, but authored pages must keep bilingual parity.

Product guide source pages for the AO and SO apphost `--guide` surfaces are authored under `docs/en/guides/` and `docs/zh-cn/guides/`. Runtime packages carry the English guide tree under `tools/<rid>/docs/en/guides/`; guide pages are not embedded in the apphost.
