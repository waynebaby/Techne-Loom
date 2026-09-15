'use strict';

const crypto = require('node:crypto');
const fs = require('node:fs');
const path = require('node:path');
const { TextDecoder } = require('node:util');

const UTF8_BOM = Buffer.from([0xef, 0xbb, 0xbf]);
const UTF8_DECODER = new TextDecoder('utf-8', { fatal: true });

function parseArguments(argumentsList) {
    const options = new Map();
    for (let index = 0; index < argumentsList.length; index += 1) {
        const argument = argumentsList[index];
        if (argument === '--') {
            continue;
        }
        if (!argument.startsWith('--') || index + 1 >= argumentsList.length) {
            throw new Error(`Expected an option followed by a value, got '${argument}'.`);
        }
        const name = argument.slice(2);
        const value = argumentsList[index + 1];
        index += 1;
        const normalizedName = name.toLowerCase();
        if (name.length === 0 || value.startsWith('--') || options.has(normalizedName)) {
            throw new Error(`Invalid or duplicated option '--${name}'.`);
        }
        options.set(normalizedName, value);
    }
    return options;
}

function requirePath(options, name) {
    const value = options.get(name);
    if (typeof value !== 'string' || value.trim().length === 0) {
        throw new Error(`Missing required option '--${name}'.`);
    }
    return path.resolve(value);
}

function hasUtf8Bom(bytes) {
    return bytes.length >= 3 && bytes[0] === 0xef && bytes[1] === 0xbb && bytes[2] === 0xbf;
}

function decodeUtf8(bytes, filePath) {
    const contentBytes = hasUtf8Bom(bytes) ? bytes.subarray(3) : bytes;
    try {
        return UTF8_DECODER.decode(contentBytes);
    } catch (error) {
        throw new Error(`File '${filePath}' is not valid UTF-8: ${error.message}`);
    }
}

function readUtf8Bytes(filePath) {
    return fs.readFileSync(filePath);
}

function readUtf8Text(filePath) {
    return decodeUtf8(readUtf8Bytes(filePath), filePath);
}

function normalize(text) {
    return text.replaceAll('\r\n', '\n').replaceAll('\r', '\n');
}

function splitLines(text) {
    let normalized = normalize(text);
    if (normalized.length === 0) {
        return [];
    }
    if (normalized.endsWith('\n')) {
        normalized = normalized.slice(0, -1);
    }
    return normalized.split('\n');
}

function detectNewline(text) {
    if (text.includes('\r\n')) {
        return '\r\n';
    }
    if (text.includes('\r')) {
        return '\r';
    }
    return '\n';
}

function getTrailingNewline(text) {
    if (text.endsWith('\r\n')) {
        return '\r\n';
    }
    if (text.endsWith('\n')) {
        return '\n';
    }
    if (text.endsWith('\r')) {
        return '\r';
    }
    return '';
}

function convertNewlines(text, newline) {
    if (newline === '\r\n') {
        return text.replaceAll('\n', '\r\n');
    }
    if (newline === '\r') {
        return text.replaceAll('\n', '\r');
    }
    return text;
}

function boundaryEquals(actual, expected) {
    return actual.trim().toLowerCase() === expected.trim().toLowerCase();
}

function requiredPositiveInteger(edit, propertyName) {
    const value = edit && edit[propertyName];
    if (!Number.isInteger(value) || value < 1) {
        throw new Error(`Edit property '${propertyName}' must be a positive integer.`);
    }
    return value;
}

function requiredPath(edit, propertyName) {
    const value = edit && edit[propertyName];
    if (typeof value !== 'string' || value.trim().length === 0) {
        throw new Error(`Edit property '${propertyName}' must be a non-empty path.`);
    }
    return path.resolve(value);
}

function readBoundary(filePath) {
    const content = readUtf8Text(filePath);
    const lines = splitLines(content);
    if (lines.length === 0 && normalize(content).length === 0) {
        return '';
    }
    if (lines.length !== 1) {
        throw new Error(`Boundary file '${filePath}' must contain exactly one logical line.`);
    }
    return lines[0];
}

function readEdits(manifestPath) {
    let manifest;
    try {
        manifest = JSON.parse(readUtf8Text(manifestPath));
    } catch (error) {
        throw new Error(`Unable to read edits manifest '${manifestPath}': ${error.message}`);
    }
    if (!manifest || typeof manifest !== 'object' || Array.isArray(manifest)
        || !Array.isArray(manifest.edits) || manifest.edits.length === 0) {
        throw new Error("The edits manifest must contain a non-empty 'edits' array.");
    }

    return manifest.edits.map((edit) => {
        const startLine = requiredPositiveInteger(edit, 'start_line');
        const endLine = requiredPositiveInteger(edit, 'end_line');
        if (endLine < startLine) {
            throw new Error(`Invalid edit range ${startLine}-${endLine}.`);
        }
        const expectedStartPath = requiredPath(edit, 'expected_start_file');
        const expectedEndPath = requiredPath(edit, 'expected_end_file');
        const replacementPath = requiredPath(edit, 'replacement_file');
        return {
            startLine,
            endLine,
            expectedStart: readBoundary(expectedStartPath),
            expectedEnd: readBoundary(expectedEndPath),
            replacementLines: splitLines(readUtf8Text(replacementPath)),
        };
    });
}

function validateRanges(edits, originalLines, targetPath) {
    const ordered = [...edits].sort((left, right) => left.startLine - right.startLine);
    for (let index = 0; index < ordered.length; index += 1) {
        const edit = ordered[index];
        if (edit.startLine > originalLines.length || edit.endLine > originalLines.length) {
            throw new Error(`Edit range ${edit.startLine}-${edit.endLine} exceeds '${targetPath}' (${originalLines.length} lines).`);
        }
        if (!boundaryEquals(originalLines[edit.startLine - 1], edit.expectedStart)
            || !boundaryEquals(originalLines[edit.endLine - 1], edit.expectedEnd)) {
            throw new Error(`Edit range ${edit.startLine}-${edit.endLine} has mismatched boundary content.`);
        }
        if (index > 0 && ordered[index - 1].endLine >= edit.startLine) {
            throw new Error(`Edit ranges ${ordered[index - 1].startLine}-${ordered[index - 1].endLine} and ${edit.startLine}-${edit.endLine} overlap.`);
        }
    }
}

function sameLines(actualLines, expectedLines) {
    return actualLines.length === expectedLines.length
        && actualLines.every((line, index) => line === expectedLines[index]);
}

function validateResult(filePath, expectedLines, expectedTrailingNewline, expectedBom) {
    const bytes = readUtf8Bytes(filePath);
    const text = decodeUtf8(bytes, filePath);
    if (!sameLines(splitLines(text), expectedLines)
        || getTrailingNewline(text) !== expectedTrailingNewline
        || hasUtf8Bom(bytes) !== expectedBom) {
        throw new Error(`Post-write validation failed for '${filePath}'.`);
    }
}

function writeUtf8File(filePath, text, withBom) {
    const content = Buffer.from(text, 'utf8');
    const bytes = withBom ? Buffer.concat([UTF8_BOM, content]) : content;
    fs.writeFileSync(filePath, bytes, { flag: 'wx' });
}

function replaceTarget(temporaryPath, targetPath) {
    fs.renameSync(temporaryPath, targetPath);
}

function main() {
    const options = parseArguments(process.argv.slice(2));
    const targetPath = requirePath(options, 'target-file');
    const editsManifestPath = requirePath(options, 'edits-file');

    const originalBytes = readUtf8Bytes(targetPath);
    const original = decodeUtf8(originalBytes, targetPath);
    const newline = detectNewline(original);
    const trailingNewline = getTrailingNewline(original);
    const originalLines = splitLines(original);
    const edits = readEdits(editsManifestPath);
    validateRanges(edits, originalLines, targetPath);

    const expectedLines = [...originalLines];
    for (const edit of [...edits].sort((left, right) => right.startLine - left.startLine)) {
        expectedLines.splice(edit.startLine - 1, edit.endLine - edit.startLine + 1, ...edit.replacementLines);
    }

    const updated = convertNewlines(expectedLines.join('\n'), newline) + trailingNewline;
    const temporaryPath = `${targetPath}.${crypto.randomUUID().replaceAll('-', '')}.range-edit.tmp`;
    try {
        writeUtf8File(temporaryPath, updated, hasUtf8Bom(originalBytes));
        validateResult(temporaryPath, expectedLines, trailingNewline, hasUtf8Bom(originalBytes));
        replaceTarget(temporaryPath, targetPath);
        validateResult(targetPath, expectedLines, trailingNewline, hasUtf8Bom(originalBytes));
        console.log(`Applied ${edits.length} line-range edits to '${targetPath}' from one original read.`);
    } finally {
        if (fs.existsSync(temporaryPath)) {
            fs.rmSync(temporaryPath);
        }
    }
}

try {
    main();
} catch (error) {
    console.error(error instanceof Error ? error.message : String(error));
    process.exitCode = 1;
}
