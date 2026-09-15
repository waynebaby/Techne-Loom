'use strict';

const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { spawnSync } = require('node:child_process');

const repositoryRoot = path.resolve(__dirname, '..', '..');
const scriptsRoot = path.join(repositoryRoot, 'scripts');
const utf8Bom = Buffer.from([0xef, 0xbb, 0xbf]);

function toWslPath(filePath) {
    const resolved = path.resolve(filePath).replaceAll('\\', '/');
    const match = /^([A-Za-z]):\/(.*)$/.exec(resolved);
    return match ? `/mnt/${match[1].toLowerCase()}/${match[2]}` : resolved;
}

const runners = [
    {
        name: 'csx',
        command: 'dotnet',
        buildArguments: (targetPath, manifestPath, extraArguments = []) => [
            'script',
            path.join(scriptsRoot, 'ApplyLineRangeEdit.csx'),
            '--',
            ...extraArguments,
            '--target-file',
            targetPath,
            '--edits-file',
            manifestPath,
        ],
    },
    {
        name: 'node',
        command: 'node',
        buildArguments: (targetPath, manifestPath, extraArguments = []) => [
            path.join(scriptsRoot, 'ApplyLineRangeEdit.js'),
            ...extraArguments,
            '--target-file',
            targetPath,
            '--edits-file',
            manifestPath,
        ],
    },
    {
        name: 'python',
        command: 'python',
        buildArguments: (targetPath, manifestPath, extraArguments = []) => [
            path.join(scriptsRoot, 'ApplyLineRangeEdit.py'),
            ...extraArguments,
            '--target-file',
            targetPath,
            '--edits-file',
            manifestPath,
        ],
    },
    {
        name: 'powershell',
        command: 'powershell.exe',
        buildArguments: (targetPath, manifestPath, extraArguments = []) => [
            '-NoProfile',
            '-NonInteractive',
            '-ExecutionPolicy',
            'Bypass',
            '-File',
            path.join(scriptsRoot, 'ApplyLineRangeEdit.ps1'),
            ...extraArguments,
            '--target-file',
            targetPath,
            '--edits-file',
            manifestPath,
        ],
    },
    {
        name: 'bash',
        command: process.platform === 'win32' ? 'wsl.exe' : 'bash',
        buildArguments: (targetPath, manifestPath, extraArguments = []) => {
            const scriptPath = process.platform === 'win32'
                ? toWslPath(path.join(scriptsRoot, 'ApplyLineRangeEdit.sh'))
                : path.join(scriptsRoot, 'ApplyLineRangeEdit.sh');
            const target = process.platform === 'win32' ? toWslPath(targetPath) : targetPath;
            const manifest = process.platform === 'win32' ? toWslPath(manifestPath) : manifestPath;
            return process.platform === 'win32'
                ? ['bash', scriptPath, ...extraArguments, '--target-file', target, '--edits-file', manifest]
                : [scriptPath, ...extraArguments, '--target-file', target, '--edits-file', manifest];
        },
    }
];

function selectedRunners() {
    const selectionArgument = process.argv.slice(2).find((argument) => argument.startsWith('--only='));
    if (!selectionArgument) {
        return runners;
    }
    const names = new Set(selectionArgument.slice('--only='.length).split(',').filter(Boolean));
    const selected = runners.filter((runner) => names.has(runner.name));
    assert.ok(selected.length > 0, `No known runner selected by '${selectionArgument}'.`);
    return selected;
}

function run(runner, targetPath, manifestPath, extraArguments = []) {
    const result = spawnSync(
        runner.command,
        runner.buildArguments(targetPath, manifestPath, extraArguments),
        {
            cwd: repositoryRoot,
            encoding: 'utf8',
            windowsHide: true,
            maxBuffer: 1024 * 1024,
        },
    );
    return {
        ...result,
        output: `${result.stdout || ''}${result.stderr || ''}`,
    };
}

function writeUtf8(filePath, text, withBom = false) {
    const content = Buffer.from(text, 'utf8');
    fs.writeFileSync(filePath, withBom ? Buffer.concat([utf8Bom, content]) : content);
}

function encodeTarget(lines, newline, trailingNewline, withBom) {
    const text = lines.join(newline) + (trailingNewline ? newline : '');
    const content = Buffer.from(text, 'utf8');
    return withBom ? Buffer.concat([utf8Bom, content]) : content;
}

function createFixture(originalBytes) {
    const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'loom-range-edit fixture '));
    const targetPath = path.join(directory, 'target file.txt');
    const manifestPath = path.join(directory, 'edits manifest.json');
    fs.writeFileSync(targetPath, originalBytes);
    return { directory, targetPath, manifestPath };
}

function writeEditFiles(directory, editIndex, startText, endText, replacementText) {
    const expectedStartPath = path.join(directory, `expected ${editIndex} start.txt`);
    const expectedEndPath = path.join(directory, `expected ${editIndex} end.txt`);
    const replacementPath = path.join(directory, `replacement ${editIndex}.txt`);
    writeUtf8(expectedStartPath, startText);
    writeUtf8(expectedEndPath, endText);
    writeUtf8(replacementPath, replacementText);
    return {
        expected_start_file: expectedStartPath,
        expected_end_file: expectedEndPath,
        replacement_file: replacementPath,
    };
}

function writeManifest(manifestPath, manifest) {
    fs.writeFileSync(manifestPath, `${JSON.stringify(manifest, null, 2)}\n`, 'utf8');
}

function removeFixture(fixture) {
    fs.rmSync(fixture.directory, { recursive: true, force: true });
}

function assertSuccess(runner, result) {
    assert.equal(
        result.status,
        0,
        `${runner.name} should succeed.\n${result.output}`,
    );
}

function assertFailure(runner, result) {
    assert.notEqual(
        result.status,
        0,
        `${runner.name} should fail.\n${result.output}`,
    );
}

function assertNoTemporaryFiles(directory) {
    const temporaryFiles = fs.readdirSync(directory).filter((name) => name.endsWith('.range-edit.tmp'));
    assert.deepEqual(temporaryFiles, [], `Temporary range-edit files remain in '${directory}'.`);
}

function runPositiveCase(runner, newline, withBom, trailingNewline) {
    const originalLines = ['Alpha', 'Beta', 'Gamma', 'Delta', 'Epsilon'];
    const expectedLines = ['Alpha', 'BETA', 'beta-tail', 'Gamma', 'DELTA', 'epsilon-tail'];
    const fixture = createFixture(encodeTarget(originalLines, newline, trailingNewline, withBom));
    try {
        const editOne = writeEditFiles(fixture.directory, 1, 'Delta', 'Epsilon', 'DELTA\nepsilon-tail');
        const editTwo = writeEditFiles(fixture.directory, 2, 'Beta', 'Beta', 'BETA\nbeta-tail');
        writeManifest(fixture.manifestPath, {
            edits: [
                { start_line: 4, end_line: 5, ...editOne },
                { start_line: 2, end_line: 2, ...editTwo },
            ],
        });
        const result = run(runner, fixture.targetPath, fixture.manifestPath);
        assertSuccess(runner, result);
        assert.deepEqual(
            fs.readFileSync(fixture.targetPath),
            encodeTarget(expectedLines, newline, trailingNewline, withBom),
            `${runner.name} produced different bytes for ${JSON.stringify({ newline, withBom, trailingNewline })}.`,
        );
        assertNoTemporaryFiles(fixture.directory);
    } finally {
        removeFixture(fixture);
    }
}

function runMixedNewlineCase(runner) {
    const fixture = createFixture(Buffer.from('Alpha\r\nBeta\rGamma\nDelta\r\n', 'utf8'));
    try {
        const edit = writeEditFiles(fixture.directory, 1, 'Beta', 'Beta', 'BETA');
        writeManifest(fixture.manifestPath, {
            edits: [{ start_line: 2, end_line: 2, ...edit }],
        });
        assertSuccess(runner, run(runner, fixture.targetPath, fixture.manifestPath));
        assert.deepEqual(
            fs.readFileSync(fixture.targetPath),
            Buffer.from('Alpha\r\nBETA\r\nGamma\r\nDelta\r\n', 'utf8'),
            `${runner.name} did not preserve the detected CRLF style for mixed input.`,
        );
        assertNoTemporaryFiles(fixture.directory);
    } finally {
        removeFixture(fixture);
    }
}

function runEmptyReplacementCase(runner) {
    const fixture = createFixture(Buffer.from('Alpha\nBeta\nGamma', 'utf8'));
    try {
        const edit = writeEditFiles(fixture.directory, 1, 'Beta', 'Beta', '');
        writeManifest(fixture.manifestPath, {
            edits: [{ start_line: 2, end_line: 2, ...edit }],
        });
        assertSuccess(runner, run(runner, fixture.targetPath, fixture.manifestPath));
        assert.deepEqual(fs.readFileSync(fixture.targetPath), Buffer.from('Alpha\nGamma', 'utf8'));
        assertNoTemporaryFiles(fixture.directory);
    } finally {
        removeFixture(fixture);
    }
}

function runEmptyLogicalLineCase(runner) {
    const fixture = createFixture(Buffer.from('\n', 'utf8'));
    try {
        const edit = writeEditFiles(fixture.directory, 1, '', '', 'replacement');
        writeManifest(fixture.manifestPath, {
            edits: [{ start_line: 1, end_line: 1, ...edit }],
        });
        assertSuccess(runner, run(runner, fixture.targetPath, fixture.manifestPath));
        assert.deepEqual(fs.readFileSync(fixture.targetPath), Buffer.from('replacement\n', 'utf8'));
        assertNoTemporaryFiles(fixture.directory);
    } finally {
        removeFixture(fixture);
    }
}

function runFailureCase(runner, name, manifest, expectedFiles = {}) {
    const originalBytes = Buffer.from('Alpha\nBeta\nGamma', 'utf8');
    const fixture = createFixture(originalBytes);
    try {
        const editFiles = writeEditFiles(
            fixture.directory,
            name,
            expectedFiles.start || 'Beta',
            expectedFiles.end || 'Beta',
            expectedFiles.replacement || 'BETA',
        );
        const preparedManifest = typeof manifest === 'function' ? manifest(editFiles) : manifest;
        if (Buffer.isBuffer(preparedManifest)) {
            fs.writeFileSync(fixture.manifestPath, preparedManifest);
        } else {
            writeManifest(fixture.manifestPath, {
                edits: preparedManifest.map((edit) => ({ ...editFiles, ...edit })),
            });
        }
        const before = fs.readFileSync(fixture.targetPath);
        const result = run(runner, fixture.targetPath, fixture.manifestPath);
        assertFailure(runner, result);
        assert.deepEqual(fs.readFileSync(fixture.targetPath), before, `${runner.name} changed the target for ${name}.`);
        assertNoTemporaryFiles(fixture.directory);
    } finally {
        removeFixture(fixture);
    }
}

function runArgumentFailureCases(runner) {
    const fixture = createFixture(Buffer.from('Alpha\nBeta\nGamma', 'utf8'));
    try {
        const edit = writeEditFiles(fixture.directory, 'argument', 'Beta', 'Beta', 'BETA');
        writeManifest(fixture.manifestPath, { edits: [{ start_line: 2, end_line: 2, ...edit }] });
        const before = fs.readFileSync(fixture.targetPath);
        assertFailure(runner, run(runner, '', '', []));
        assertFailure(runner, run(runner, fixture.targetPath, fixture.manifestPath, ['--target-file', fixture.targetPath]));
        assert.deepEqual(fs.readFileSync(fixture.targetPath), before);
        assertNoTemporaryFiles(fixture.directory);
    } finally {
        removeFixture(fixture);
    }
}

function runContractForRunner(runner) {
    for (const newline of ['\n', '\r\n', '\r']) {
        for (const withBom of [false, true]) {
            for (const trailingNewline of [false, true]) {
                runPositiveCase(runner, newline, withBom, trailingNewline);
            }
        }
    }
    runMixedNewlineCase(runner);
    runEmptyReplacementCase(runner);
    runEmptyLogicalLineCase(runner);
    runFailureCase(runner, 'empty', []);
    runFailureCase(runner, 'reverse', [{ start_line: 2, end_line: 1 }]);
    runFailureCase(runner, 'out-of-range', [{ start_line: 4, end_line: 4 }]);
    runFailureCase(runner, 'mismatch', [{ start_line: 2, end_line: 2 }], { start: 'Wrong', end: 'Wrong' });
    runFailureCase(runner, 'overlap', [
        { start_line: 1, end_line: 2 },
        { start_line: 2, end_line: 3, expected_start_file: '', expected_end_file: '' },
    ]);
    runFailureCase(runner, 'boundary-lines', (editFiles) => {
        const multiLineBoundaryPath = path.join(path.dirname(editFiles.expected_start_file), 'multi-line boundary.txt');
        writeUtf8(multiLineBoundaryPath, 'Beta\nExtra');
        return [{ start_line: 2, end_line: 2, expected_start_file: multiLineBoundaryPath }];
    });
    runFailureCase(runner, 'invalid-line', [{ start_line: '2', end_line: 2 }]);
    runFailureCase(runner, 'invalid-utf8', Buffer.from([0xff, 0xfe]));
    runArgumentFailureCases(runner);
}

function main() {
    const selected = selectedRunners();
    for (const runner of selected) {
        runContractForRunner(runner);
        console.log(`PASS ${runner.name}`);
    }
    console.log(`Validated ${selected.length} ApplyLineRangeEdit runtime(s).`);
}

main();
