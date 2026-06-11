# Contributing

[中文](CONTRIBUTING.zh-CN.md)

Techne Loom is being opened in staged slices. Contributions are welcome, but the repository is still converging on its first public package and protocol boundary.

## Current Contribution Rules

- Keep AO and SO conceptually separate. Do not collapse them into one runtime or describe one as the other.
- Preserve the package-first layout under `src/dotnet`, `src/nodejs`, and `src/python`.
- Treat `/docs/en` and `/docs/zh-cn` as mirrored trees. New public docs must land in both languages before merge.
- Keep product guides authored under `/docs/<lang>/reference/products/` so `dotnet ao.dll --guide` and `dotnet so.dll --guide` can stay version-matched.
- Prefer narrow, reviewable slices. Major slices should pass review before the next slice starts.

## Development Flow

1. Read the repository rules in `AGENTS.md`.
2. Make the smallest change that closes a concrete gap.
3. Add or update tests for changed behavior.
4. Run the narrowest validation that can falsify the change.
5. Keep public docs aligned with code and package boundaries.

## Pull Request Expectations

- Explain the product boundary impact.
- Call out package or CLI contract changes.
- Note documentation updates.
- Include validation evidence.

Long-form contribution policy lives under `docs/en/governance/policies/contributing.md`.
