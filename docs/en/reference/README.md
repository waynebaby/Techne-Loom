# Reference

[中文](../../zh-cn/reference/README.md)

Reference pages document the current public contract surface, not just aspirational design notes.

If another agent is continuing implementation work, this section should be read together with the architecture roadmap and the product guides before making code changes.

## Sections

- [CLI Reference](cli.md)
- [Configuration](configuration.md)
- [Workflow Schema](workflow-schema.md)
- [Exit Codes](exit-codes.md)
- [Package References](packages/)
- [Product Guides](products/)

## Continuation Order

For implementation handoff work, read in this order:

1. [Product Guides](products/)
2. [Workflow Schema](workflow-schema.md)
3. [CLI Reference](cli.md)
4. [Configuration](configuration.md)
5. [Exit Codes](exit-codes.md)

## Interpretation Rule

- If docs, code, and tests disagree, reviewed code plus tests win for the current implemented slice.
- The purpose of this reference section is to keep those disagreements as small and visible as possible.
