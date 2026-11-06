# Narrow SO checksum-policy wording revision

## Scope
Update only source-policy strings in `assets/so-workflow/so-template.json` and `assets/so-workflow/contract.json`. Keep all nodes, fields, package identity/version/RID, package safety, extraction, apphost, and execution semantics unchanged.

## Authority and route
Use the exact published SO 0.4.17 `win-x64` apphost, its fresh guide, generated schema/demo and successful same-apphost demo compile; follow the local `loom-skill-enhancement Workflow Designer` contract by path-only driver fallback.

## Validation
Prepare an external candidate and supply the existing string `context.target_skill_path` in that candidate only so SO can load the owning contract. Compile that exact candidate with the same apphost and external audit root. Treat compile as structural evidence only; do not run the business workflow or claim new runtime semantics. Verify both checked-in JSON files parse and that the final Git diff is restricted to the two requested paths.
