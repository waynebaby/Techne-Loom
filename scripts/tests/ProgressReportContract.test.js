'use strict';

const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');

const repositoryRoot = path.resolve(__dirname, '..', '..');

function read(relativePath) {
    return fs.readFileSync(path.join(repositoryRoot, relativePath), 'utf8');
}

const deliveryGuides = [
    '.agents/skills/loom-plan-execution/reference/mermaid-artifact-delivery.md',
    '.agents/skills/loom-skill-enhancement/reference/mermaid-artifact-delivery.md',
];

for (const relativePath of deliveryGuides) {
    test(`${relativePath} defines append-only progress-report logging`, () => {
        const text = read(relativePath);
        assert.match(text, /## Per-Execution Progress Log/);
        assert.match(text, /<execution-output-root>\/progress-report\.md/);
        assert.match(text, /appends? the complete Markdown entry serially|append each complete post-execution Markdown progress report serially/i);
        assert.match(text, /ISO 8601 UTC timestamp/);
        assert.match(text, /numeric UTC offset and OS timezone ID/);
        assert.match(text, /preserve the exact prior log/);
        assert.match(text, /pause later business-step progression/);
        assert.match(text, /Do not change or roll back runtime workflow state/);
    });
}

test('SO enhancement preflight requires target progress-report logging', () => {
    const contract = JSON.parse(read('.agents/skills/loom-skill-enhancement/assets/so-workflow/contract.json'));
    const policy = contract.default_assumptions.target_skill_progress_report_policy;
    const outputEvidence = contract.outputs.target_skill_progress_report_policy;
    const reviewContract = read('.agents/skills/loom-skill-enhancement/reference/review-and-evidence-contract.md');

    assert.match(contract.default_assumptions.progress_report_log_contract, /progress-report\.md/);
    assert.match(policy, /progress-report\.md/);
    assert.match(policy, /OS timezone ID/);
    assert.match(outputEvidence, /progress entries/);
    assert.match(reviewContract, /target `SKILL\.md` per-execution progress-log wording/);
});

test('AO contract keeps per-execution progress-report logging', () => {
    const contract = JSON.parse(read('.agents/skills/loom-plan-execution/contract.json'));
    const policy = contract.default_assumptions.progress_report_log_contract;

    assert.match(policy, /progress-report\.md/);
    assert.match(policy, /ISO 8601 UTC/);
    assert.match(policy, /OS timezone ID/);
    assert.match(policy, /preserve the prior log/);
    assert.match(policy, /without changing runtime execution state/);
});
