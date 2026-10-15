'use strict';

const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');

const repositoryRoot = path.resolve(__dirname, '..', '..');

function read(relativePath) {
    return fs.readFileSync(path.join(repositoryRoot, relativePath), 'utf8');
}

const englishRuleFiles = [
    '.agents/skills/loom-plan-execution/SKILL.md',
    '.agents/skills/loom-plan-execution/reference/ao-skill-reference.md',
    '.agents/skills/loom-skill-enhancement/SKILL.md',
    '.agents/skills/loom-skill-enhancement/reference/review-and-evidence-contract.md',
    '.github/instructions/loom-skill-governance.instructions.md',
    'docs/en/guides/skill-usage.md',
];

const sharedTokens = ['`agent not found`', 'unknown agent', '`agentName`', 'path-only'];

for (const relativePath of englishRuleFiles) {
    test(`${relativePath} states the full named-agent fallback`, () => {
        const text = read(relativePath).toLowerCase();
        for (const token of [...sharedTokens, 'driver fallback does not require user approval']) {
            assert.ok(text.includes(token.toLowerCase()), `missing "${token}"`);
        }
    });
}

test('zh-cn skill usage guide states the full named-agent fallback', () => {
    const text = read('docs/zh-cn/guides/skill-usage.md');
    for (const token of [...sharedTokens, '回退到 driver 不需要用户批准']) {
        assert.ok(text.includes(token), `missing "${token}"`);
    }
});

test('both skills keep the dispatch preflight, transport retry, and dispatch record', () => {
    for (const relativePath of ['.agents/skills/loom-plan-execution/SKILL.md', '.agents/skills/loom-skill-enhancement/SKILL.md']) {
        const text = read(relativePath);
        assert.match(text, /## Named Agent Resolution/);
        assert.match(text, /Preflight before dispatch/);
        assert.match(text, /HTTP 408, or timeout is a transport failure, not a missing agent/);
        assert.match(text, /route \(`direct` or `driver-fallback`\), delivery mode \(`inline` or `path-only`\)/);
        assert.match(text, /never substitute a similarly named role/);
    }
});

test('plan-execution drops the narrow trigger and the path ban', () => {
    for (const relativePath of [
        '.agents/skills/loom-plan-execution/SKILL.md',
        '.agents/skills/loom-plan-execution/reference/ao-skill-reference.md',
        '.agents/skills/loom-skill-enhancement/SKILL.md',
    ]) {
        const text = read(relativePath);
        assert.doesNotMatch(text, /If the host responds `agent not found`/);
        assert.doesNotMatch(text, /Never pass only a path/);
        assert.doesNotMatch(text, /A direct\/manual fallback requires explicit user approval/);
    }
});

test('skill usage guides link to both Named Agent Resolution sections', () => {
    const links = [
        '../../../.agents/skills/loom-plan-execution/SKILL.md#named-agent-resolution',
        '../../../.agents/skills/loom-skill-enhancement/SKILL.md#named-agent-resolution',
    ];
    for (const language of ['en', 'zh-cn']) {
        const guidePath = path.join(repositoryRoot, 'docs', language, 'guides', 'skill-usage.md');
        const guide = fs.readFileSync(guidePath, 'utf8');
        for (const link of links) {
            assert.ok(guide.includes(`](${link})`), `${language} guide is missing ${link}`);
            const target = path.resolve(path.dirname(guidePath), link.split('#')[0]);
            assert.match(fs.readFileSync(target, 'utf8'), /^## Named Agent Resolution$/m);
        }
    }
});
