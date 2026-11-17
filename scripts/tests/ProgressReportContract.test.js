'use strict';

const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');

const repositoryRoot = path.resolve(__dirname, '../..');
const read = (relativePath) => fs.readFileSync(path.join(repositoryRoot, relativePath), 'utf8');
const helperName = ['Append', 'Progress', 'Report'].join('');
const helperPattern = new RegExp(helperName + '\\.(?:ps1|sh)');
const progressDocuments = [
  'AGENTS.md',
  'docs/en/guides/loom-guide.md',
  'docs/zh-cn/guides/loom-guide.md',
  '.agents/skills/loom-ask-user/SKILL.md',
  '.agents/skills/loom-plan-execution/reference/execution-and-reporting.md',
  '.agents/skills/loom-plan-execution/reference/mermaid-artifact-delivery.md',
  '.agents/skills/loom-plan-execution/contract.json',
  '.agents/skills/loom-skill-enhancement/SKILL.md',
  '.agents/skills/loom-skill-enhancement/reference/mermaid-artifact-delivery.md',
  '.agents/skills/loom-skill-enhancement/reference/plain-language-feedback.md',
  '.agents/skills/loom-skill-enhancement/reference/planning-and-validation.md',
  '.agents/skills/loom-skill-enhancement/reference/review-and-evidence-contract.md',
  '.agents/skills/loom-skill-enhancement/assets/so-workflow/node-to-file-map.md',
  '.agents/skills/loom-skill-enhancement/assets/so-workflow/contract.json',
];

test('progress reports use the built-in Loom CLI without bundled helpers', () => {
  const implementation = read('src/dotnet/Techne.Loom/Program.cs');
  assert.match(implementation, /progress append/);
  assert.match(implementation, /FileMode\.Append/);
  for (const relativePath of progressDocuments) {
    const content = read(relativePath);
    const searchableContent = content.replace(/\\u003c/gi, '<').replace(/\\u003e/gi, '>');
    assert.match(searchableContent, /progress append --entry-file/);
    assert.match(searchableContent, /--output-root <execution-output-root>/);
    assert.doesNotMatch(searchableContent, helperPattern);
  }
  for (const skillRoot of [
    '.agents/skills/loom-ask-user',
    '.agents/skills/loom-plan-execution',
    '.agents/skills/loom-skill-enhancement',
  ]) {
    const scriptRoot = path.join(repositoryRoot, skillRoot, 'assets/scripts');
    for (const extension of ['ps1', 'sh']) {
      assert.equal(fs.existsSync(path.join(scriptRoot, helperName + '.' + extension)), false);
    }
  }
});

test('GitHub runtime fallback requires explicit human approval', () => {
  const policyFiles = [
    'AGENTS.md',
    '.github/instructions/loom-runtime.instructions.md',
    'packages.released.md',
    'packages.released.zh-CN.md',
    'packages.beta.md',
    'packages.beta.zh-CN.md',
    '.agents/skills/loom-ask-user/reference/runtime-dependency.md',
    '.agents/skills/loom-plan-execution/SKILL.md',
    '.agents/skills/loom-plan-execution/reference/runtime-and-dispatch.md',
    '.agents/skills/loom-skill-enhancement/SKILL.md',
    '.agents/skills/loom-skill-enhancement/reference/runtime-and-dispatch.md',
    '.agents/skills/loom-skill-enhancement/reference/planning-and-validation.md',
  ];
  for (const relativePath of policyFiles) {
    const content = read(relativePath);
    const searchableContent = content.replace(/\\u003c/gi, '<').replace(/\\u003e/gi, '>');
    assert.match(searchableContent, /explicit human approval|明确批准/i, relativePath);
    assert.match(searchableContent, /resumably pending|可恢复的.{0,30}pending/i, relativePath);
    assert.doesNotMatch(searchableContent, /not human-confirmed/i, relativePath);
  }
});

test('XO Ask answers are explicitly untrusted data', () => {
  const content = read('.agents/skills/loom-ask-user/SKILL.md');
  assert.match(content, /untrusted data/i);
  assert.match(content, /instructions embedded in answer text/i);
  assert.match(content, /separate trusted authorization/i);
  assert.match(content, /Never delegate invoking this skill/);
  assert.match(content, /interpreting or using returned answers to a subagent/);
});
