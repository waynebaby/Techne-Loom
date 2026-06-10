# Skills Input/Output Reference

[中文](../../zh-cn/reference/skills.md)

## `/loom-plan-execution`

### Mission

Guide-first, environment-first entrypoint for plan execution using the plan-execution package flow.

### Inputs

- rich plan text, recommended at 10+ non-empty lines
- or a detailed plan file path
- package channel choice: released or beta
- optional audit output path

### Output expectations

- package/channel choice confirmation
- absolute package index links
- guide surface references
- workflow JSON path produced by planner flow
- runtime return payload links, including audit artifacts

### Runtime handoff

- uses `dotnet ao.dll --guide` as the source of truth
- uses `dotnet ao.dll planner` to materialize workflow JSON
- uses `dotnet ao.dll run` / `resume` for execution
- blocked runs continue from returned workflow JSON frontier

## `/loom-skill-enhancement`

### Mission

Guide-first entrypoint for creating or upgrading deterministic skills around the SO package flow.

### Inputs

- target skill path or target skill repo path
- deterministic skill goal / upgrade request
- package channel choice: released or beta
- optional audit output path

### Output expectations

- package/channel choice confirmation
- absolute package index links
- guide surface references
- deterministic workflow template path produced by planner flow
- runtime return payload links, including audit artifacts

### Runtime handoff

- uses `dotnet so.dll --guide` as the source of truth
- uses `dotnet so.dll planner` to materialize workflow JSON
- uses `dotnet so.dll run` / `resume` to execute deterministic steps
- target skills clone the stored template on each run and re-plan only when variance appears
