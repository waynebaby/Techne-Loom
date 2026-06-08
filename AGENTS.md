# Workspace Agent Rules

<!-- cto-skills-manager-managed:begin -->
## Shared Python Environment

This workspace uses the shared virtual environment pointer from `.venv.path`.

- Managed by `cto-skills-manager`.
- Windows: resolve `.venv.path` with PowerShell before invoking Python-based tooling.
- If a Python runtime is available but the `.venv.path` target does not exist yet, initialize that virtual environment first and then use the new environment.
- Linux: resolve `.venv.path` with bash before invoking Python-based tooling.

## Run Output Naming

- When a skill creates a per-run output root, keep the skill-owned parent directory and name the run root `exec-<YYYYMMDD_HHMMSS>-<skill-slug>-result/`.
- Keep the timestamp immediately after `exec-` so runs remain sortable even when adjacent steps switch skills.
<!-- cto-skills-manager-managed:end -->
