'use strict';

const assert = require('node:assert/strict');
const { spawnSync } = require('node:child_process');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const test = require('node:test');

const repositoryRoot = path.resolve(__dirname, '..', '..');

function read(relativePath) {
    return fs.readFileSync(path.join(repositoryRoot, relativePath), 'utf8');
}

function fileUrl(filePath) {
    const encodedPath = path.resolve(filePath)
        .replace(/\\/g, '/')
        .replace(/%/g, '%25')
        .replace(/ /g, '%20')
        .replace(/#/g, '%23')
        .replace(/\?/g, '%3F');
    return `file://${encodedPath}`;
}

const deliveryGuides = [
    '.agents/skills/loom-plan-execution/reference/mermaid-artifact-delivery.md',
    '.agents/skills/loom-skill-enhancement/reference/mermaid-artifact-delivery.md',
];

for (const relativePath of deliveryGuides) {
    test(`${relativePath} defines script-backed write-only logging and full file URLs`, () => {
        const text = read(relativePath);
        assert.match(text, /## Per-Execution Progress Log/);
        assert.match(text, /<execution-output-root>\/progress-report\.md/);
        assert.match(text, /AppendProgressReport\.ps1/);
        assert.match(text, /AppendProgressReport\.sh/);
        assert.match(text, /full file URL/i);
        assert.match(text, /Never read, open, or inspect the existing.*progress-report\.md/i);
        assert.match(text, /file:\/\/C:\/\.\.\./);
        assert.match(text, /file:\/\/\/\.\.\./);
        const normalizedText = text.replace(/\r\n/g, '\n');
        for (const [label, fileName] of [
            ['Mermaid', 'workflow.mermaid.md'],
            ['HTML', 'workflow.html'],
            ['Analysis', 'workflow.analysis.json'],
            ['Dataflow', 'workflow.dataflow.json'],
        ]) {
            const sampleUrl = `file://C:/path/to/${fileName}`;
            const samplePair = `[${label}](${sampleUrl})\n\`\`\`text\n${sampleUrl}\n\`\`\``;
            assert.ok(
                normalizedText.includes(samplePair),
                `${relativePath} ${label} sample must repeat the same file URL in its link and fence.`
            );
        }
        assert.match(text, /active host/i);
        assert.match(text, /encode .*spaces? .*%20/i);
        assert.match(text, /preserve the exact prior log/);
        assert.match(text, /pause later business-step progression/);
    });
}

const skillInstructions = [
    '.agents/skills/loom-plan-execution/SKILL.md',
    '.agents/skills/loom-skill-enhancement/SKILL.md',
    '.agents/skills/loom-ask-user/SKILL.md',
];

for (const relativePath of skillInstructions) {
    test(`${relativePath} invokes the platform append helper without reading the log`, () => {
        const text = read(relativePath);
        assert.match(text, /AppendProgressReport\.ps1/);
        assert.match(text, /AppendProgressReport\.sh/);
        assert.match(text, /full file URL/i);
        assert.match(text, /Never read, open, or inspect the existing `progress-report\.md`/i);
        if (!relativePath.endsWith('loom-ask-user/SKILL.md')) {
            assert.match(text, /file:\/\/C:/);
            assert.match(text, /file:\/\/\//);
        }
    });
}

test('global rules require full file URLs and platform append helpers', () => {
    const text = read('AGENTS.md');
    const docsRules = read('.github/instructions/loom-documentation.instructions.md');
    assert.match(docsRules, /file:\/\/C:/);
    assert.match(docsRules, /file:\/\/\//);
    assert.match(docsRules, /active host/i);
    assert.match(text, /file:\/\/C:/);
    assert.match(text, /file:\/\/\//);
    assert.match(text, /active host/i);
    assert.match(text, /AppendProgressReport\.ps1/);
    assert.match(text, /AppendProgressReport\.sh/);
    assert.match(text, /Never read, open, or inspect the existing `progress-report\.md`/i);
    for (const guidePath of [
        'docs/en/guides/ao-guide-reference-contracts.md',
        'docs/zh-cn/guides/ao-guide-reference-contracts.md',
        'docs/en/guides/so-guide-reference-contracts.md',
        'docs/zh-cn/guides/so-guide-reference-contracts.md',
    ]) {
        const guide = read(guidePath);
        assert.match(guide, /file:\/\/C:/);
        assert.match(guide, /file:\/\/\//);
        assert.match(guide, /%20/);
    }
});

test('SO enhancement preflight requires script-backed progress logging and usable file URLs', () => {
    const contract = JSON.parse(read('.agents/skills/loom-skill-enhancement/assets/so-workflow/contract.json'));
    const policy = contract.default_assumptions.target_skill_progress_report_policy;
    const outputEvidence = contract.outputs.target_skill_progress_report_policy;
    const reviewContract = read('.agents/skills/loom-skill-enhancement/reference/review-and-evidence-contract.md');
    const thinkOutLoud = contract.default_assumptions.think_out_loud_contract;
    const mustShowFiles = contract.default_assumptions.must_show_to_user_files_rule;

    assert.match(contract.default_assumptions.progress_report_log_contract, /AppendProgressReport\.ps1/);
    assert.match(contract.default_assumptions.progress_report_log_contract, /AppendProgressReport\.sh/);
    assert.match(contract.default_assumptions.progress_report_log_contract, /Never read, open, or inspect/);
    assert.match(contract.default_assumptions.progress_report_log_contract, /full file URL/);
    assert.match(policy, /progress-report\.md/);
    assert.match(policy, /AppendProgressReport\.ps1/);
    assert.match(policy, /AppendProgressReport\.sh/);
    assert.match(policy, /Never read, open, inspect, or copy/);
    assert.match(outputEvidence, /AppendProgressReport\.ps1/);
    assert.match(outputEvidence, /AppendProgressReport\.sh/);
    assert.match(thinkOutLoud, /file:\/\/C:/);
    assert.match(thinkOutLoud, /file:\/\/\//);
    assert.match(mustShowFiles, /active host/);
    assert.match(reviewContract, /file:\/\/C:/);
    assert.match(reviewContract, /target `SKILL\.md` per-execution progress-log wording/);
});

test('AO contract requires the platform append helper and a write-only target', () => {
    const contract = JSON.parse(read('.agents/skills/loom-plan-execution/contract.json'));
    const policy = contract.default_assumptions.progress_report_log_contract;

    assert.match(policy, /progress-report\.md/);
    assert.match(policy, /AppendProgressReport\.ps1/);
    assert.match(policy, /AppendProgressReport\.sh/);
    assert.match(policy, /full file URL/);
    assert.match(policy, /Never read, open, or inspect/);
    assert.match(policy, /without changing runtime execution state/);
    assert.match(contract.default_assumptions.think_out_loud_contract, /file:\/\/C:/);
    assert.match(contract.default_assumptions.think_out_loud_contract, /file:\/\/\//);
});

test('append helper source reads only the entry and opens the target for append', () => {
    const powerShellWriter = read('scripts/AppendProgressReport.ps1');
    const bashWriter = read('scripts/AppendProgressReport.sh');

    assert.match(powerShellWriter, /FileMode\]::Append/);
    assert.match(powerShellWriter, /ReadAllBytes\(\$entryPath\)/);
    assert.doesNotMatch(powerShellWriter, /ReadAll(?:Bytes|Text)\(\$targetPath\)/i);
    assert.match(powerShellWriter, /Write-Output \('file:\/\//);
    assert.match(bashWriter, /cat "\$entry_file"/);
    assert.match(bashWriter, />> "\$target_file"/);
    assert.match(powerShellWriter, /FileShare\]::Read/);
    assert.match(bashWriter, /mkdir "\$lock_directory"/);
    assert.match(bashWriter, /trap cleanup_lock EXIT/);
    assert.doesNotMatch(bashWriter, /cat "\$target_file"|<\s*"\$target_file"/);
    assert.match(bashWriter, /printf 'file:\/\/%s\\n'/);
});

const writerRunners = [
    {
        name: 'PowerShell',
        command: process.platform === 'win32' ? 'powershell.exe' : 'pwsh',
        probeArguments: ['-NoProfile', '-Command', 'exit 0'],
        helperPath: 'scripts/AppendProgressReport.ps1',
        buildArguments(helper, target, entry) {
            return ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', helper, '-TargetFile', target, '-EntryFile', entry];
        },
        skipped: false,
    },
    {
        name: 'Bash',
        command: 'bash',
        probeArguments: ['--version'],
        helperPath: 'scripts/AppendProgressReport.sh',
        buildArguments(helper, target, entry) {
            return [helper, '--target-file', target, '--entry-file', entry];
        },
        skipped: process.platform === 'win32',
    },
];

for (const writer of writerRunners) {
    test(`${writer.name} helper preserves prior bytes and returns a full file URL`, (context) => {
        if (writer.skipped) {
            context.skip('Run the Bash helper in WSL or a Unix host.');
            return;
        }

        const probe = spawnSync(writer.command, writer.probeArguments, { stdio: 'ignore' });
        if (probe.error && probe.error.code === 'ENOENT') {
            context.skip(`${writer.command} is unavailable on this host.`);
            return;
        }
        assert.equal(probe.status, 0, `${writer.command} probe failed.`);

        const temporaryRoot = fs.mkdtempSync(path.join(os.tmpdir(), 'loom-progress-report-'));
        try {
            const targetDirectory = path.join(temporaryRoot, 'Folder With Spaces');
            fs.mkdirSync(targetDirectory);
            const targetFile = path.join(targetDirectory, 'progress-report.md');
            const entryFile = path.join(temporaryRoot, 'entry.md');
            const previousBytes = Buffer.from('Previous bytes without newline.', 'utf8');
            const entryBytes = Buffer.from('## New entry\r\n\r\nDetails.\r\n', 'utf8');
            fs.writeFileSync(targetFile, previousBytes);
            fs.writeFileSync(entryFile, entryBytes);

            const helperPath = path.join(repositoryRoot, writer.helperPath);
            const result = spawnSync(writer.command, writer.buildArguments(helperPath, targetFile, entryFile), { encoding: 'utf8' });
            assert.equal(result.status, 0, result.stderr || result.error?.message || `${writer.name} helper failed.`);

            const expectedBytes = Buffer.concat([previousBytes, Buffer.from('\n'), entryBytes]);
            assert.deepEqual(fs.readFileSync(targetFile), expectedBytes);
            const returnedUrl = result.stdout.trimEnd();
            assert.equal(returnedUrl, fileUrl(targetFile));
            assert.doesNotMatch(returnedUrl, / /);
            assert.match(returnedUrl, /Folder%20With%20Spaces/);
        }
        finally {
            fs.rmSync(temporaryRoot, { recursive: true, force: true });
        }
    });
}
