# SO Runtime Contract Reference Copy

<!-- loom-document-copy:start -->
- source_document: `docs/en/guides/so-guide-reference-contracts.md`
- source_product: `so`
- source_channel: `beta`
- source_version: `0.3.249-beta`
- source_sha256: `f901784e2b0b58d4eb59f51596b2ce3b7433f0fbd87221778cc758d29b559ae6`
- target_bound_version: `0.2.118-beta`
- content_mode: `controlled-excerpt`
- artifact_origin: `verified-copy`
- authority_scope: `historical target-local context/reference only; the historical bound runtime evidence remains authoritative for this demo record`
- refresh_policy: `historical-frozen; update only when the demo record is intentionally revised`
<!-- loom-document-copy:end -->

This target-local excerpt records the SO contract facts used by this historical enhancement demo. It does not replace the runtime guide captured by the historical enhancement pass.

## Contract Facts

- `dotnet so.dll --guide` returns the runtime version, docs root, and guide path from the complete package docs tree.
- The returned guide path is the authority for that exact runtime. A missing guide path is failed evidence.
- Official runs use an external workflow copy, and resume continues against that same persisted copy.
- Blocked and failed states must remain explicit; they cannot be reported as completed work.
