#!/usr/bin/env node
'use strict';

const fs = require('fs');
const os = require('os');
const path = require('path');
const crypto = require('crypto');
const zlib = require('zlib');

const args = parseArgs(process.argv.slice(2));
const packageLockFile = requireValue(args, '--package-lock-file');
const mode = args['--mode'] || 'self-contained';
const noDownload = Boolean(args['--no-download']);
const cacheRoot = path.resolve(args['--package-cache-root'] || process.env.NUGET_PACKAGES || path.join(os.homedir(), '.nuget', 'packages'));
const descriptorPath = args['--runtime-descriptor-file'] ? path.resolve(args['--runtime-descriptor-file']) : null;
const descriptor = descriptorPath ? readJson(descriptorPath) : null;
const lock = readJson(path.resolve(packageLockFile));
const version = exactVersion(lock.resolved_version);

if (!['self-contained', 'dotnet-cli'].includes(mode)) {
  fail(`Unsupported runtime mode '${mode}'. Use self-contained by default or explicit dotnet-cli.`);
}
if (descriptor) {
  if (descriptor.product !== 'so') fail('The runtime descriptor must belong to product so.');
  if (descriptor.resolved_runtime_version !== version) fail(`The runtime descriptor version '${descriptor.resolved_runtime_version}' does not match lock version '${version}'.`);
  const descriptorMode = descriptor.runtime_mode === 'framework-dependent' ? 'dotnet-cli' : descriptor.runtime_mode;
  if (descriptorMode !== mode) fail(`The runtime descriptor mode '${descriptorMode}' does not match requested mode '${mode}'.`);
}

const rid = descriptor?.rid || args['--runtime-identifier'];
if (mode === 'self-contained' && !rid) {
  fail('Self-contained restore requires the resolver-selected runtime identifier or --runtime-identifier.');
}
if (rid && !/^(win|linux|osx)-(x64|arm64)$|^linux-musl-(x64|arm64)$/.test(rid)) {
  fail(`Unsupported runtime identifier '${rid}'.`);
}

const packageIds = mode === 'self-contained'
  ? [`Techne.Loom.SkillOrchestrator.Runtime.${rid}`]
  : ['Techne.Loom.SkillOrchestrator', 'Techne.Loom.Common', 'Techne.Loom.Abstractions'];
const packageResults = [];
const misses = [];

for (const packageId of packageIds) {
  const candidates = findCandidates(cacheRoot, packageId, version);
  let valid = null;
  const invalid = [];
  for (const candidate of candidates) {
    try {
      valid = validatePackage(candidate, packageId, version, mode === 'self-contained' ? rid : null);
      break;
    } catch (error) {
      invalid.push({ path: candidate, reason: error.message });
    }
  }
  if (valid) {
    packageResults.push({ package_id: packageId, resolved_version: version, cache_status: 'reused', path: valid.path, validation: valid.reason });
  } else {
    misses.push({ package_id: packageId, resolved_version: version, invalid_candidates: invalid });
    packageResults.push({ package_id: packageId, resolved_version: version, cache_status: 'missing_or_invalid', validation: 'exact-package-inspection-failed' });
  }
}

if (misses.length > 0 && noDownload) {
  writeResult({
    status: 'package_cache_invalid',
    runtime_mode: mode,
    rid: rid || null,
    resolved_runtime_version: version,
    runtime_bundle_packages: packageIds,
    package_cache_root: cacheRoot,
    cache_hit: false,
    downloaded_packages: [],
    cache_validation: { status: 'failed', reason: mode === 'self-contained' ? 'exact-rid-self-contained-package-missing-or-invalid' : 'dotnet-cli-runtime-bundle-missing-or-invalid', misses },
    package_results: packageResults,
  });
  process.exit(3);
}

const downloaded = [];
for (const miss of misses) {
  const packageId = miss.package_id;
  const downloadedPackage = downloadExact(packageId, version);
  const packageBytes = downloadedPackage.bytes;
  const validation = validatePackageBytes(packageBytes, packageId, version, mode === 'self-contained' ? rid : null);
  const destination = expectedPackagePath(cacheRoot, packageId, version);
  fs.mkdirSync(path.dirname(destination), { recursive: true });
  const temporary = `${destination}.${process.pid}.${crypto.randomUUID()}.download`;
  fs.writeFileSync(temporary, packageBytes);
  fs.renameSync(temporary, destination);
  fs.writeFileSync(`${destination}.sha512`, downloadedPackage.sha512 + '\n', 'utf8');
  const packageResult = packageResults.find(item => item.package_id === packageId);
  if (packageResult) {
    packageResult.cache_status = 'downloaded';
    packageResult.path = destination;
    packageResult.validation = validation.reason;
  }
  downloaded.push({ package_id: packageId, resolved_version: version, path: destination, url: downloadedPackage.url, sha512: downloadedPackage.sha512 });
}

const finalValidation = packageIds.map(packageId => {
  const candidate = findCandidates(cacheRoot, packageId, version)[0];
  if (!candidate) fail(`Exact package '${packageId}' remains unavailable at '${version}'.`);
  const validation = validatePackage(candidate, packageId, version, mode === 'self-contained' ? rid : null);
  return { package_id: packageId, resolved_version: version, path: candidate, validation: validation.reason };
});

writeResult({
  status: 'package_cache_ready',
  runtime_mode: mode,
  rid: rid || null,
  resolved_runtime_version: version,
  runtime_bundle_packages: packageIds,
  runtime_package_id: mode === 'self-contained' ? packageIds[0] : null,
  package_cache_root: cacheRoot,
  cache_hit: misses.length === 0,
  downloaded_packages: downloaded,
  cache_validation: {
    status: 'passed',
    policy: mode === 'self-contained' ? 'exact-rid-self-contained-package' : 'explicit-dotnet-cli-runtime-bundle',
    package_count: finalValidation.length,
    packages: finalValidation,
  },
  package_results: packageResults,
  runtime_descriptor_file: descriptorPath,
});

function parseArgs(values) {
  const result = {};
  for (let index = 0; index < values.length; index += 1) {
    const value = values[index];
    if (value === '--no-download') {
      result[value] = true;
    } else if (value.startsWith('--')) {
      result[value] = values[++index];
    } else {
      fail(`Unexpected argument '${value}'.`);
    }
  }
  return result;
}

function requireValue(values, key) {
  if (!values[key]) fail(`Missing required argument '${key}'.`);
  return values[key];
}

function exactVersion(value) {
  if (typeof value !== 'string' || !/^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$/.test(value)) {
    fail(`Package lock resolved_version must be one exact semantic version; received '${value}'.`);
  }
  return value;
}

function readJson(filePath) {
  if (!fs.existsSync(filePath)) fail(`JSON input '${filePath}' does not exist.`);
  try {
    return JSON.parse(fs.readFileSync(filePath, 'utf8'));
  } catch (error) {
    fail(`JSON input '${filePath}' is invalid: ${error.message}`);
  }
}

function expectedPackagePath(root, packageId, packageVersion) {
  return path.join(root, packageId.toLowerCase(), packageVersion.toLowerCase(), `${packageId}.${packageVersion}.nupkg`);
}

function findCandidates(root, packageId, packageVersion) {
  const directory = path.join(root, packageId.toLowerCase(), packageVersion.toLowerCase());
  const names = [`${packageId}.${packageVersion}.nupkg`, `${packageId.toLowerCase()}.${packageVersion.toLowerCase()}.nupkg`];
  const candidates = names.map(name => path.join(directory, name));
  if (fs.existsSync(directory)) {
    for (const name of fs.readdirSync(directory)) {
      if (name.toLowerCase().endsWith('.nupkg')) candidates.push(path.join(directory, name));
    }
  }
  return [...new Set(candidates)].filter(filePath => fs.existsSync(filePath) && fs.statSync(filePath).isFile());
}

function validatePackage(filePath, packageId, packageVersion, rid) {
  const bytes = fs.readFileSync(filePath);
  const result = validatePackageBytes(bytes, packageId, packageVersion, rid);
  const hashFile = `${filePath}.sha512`;
  if (fs.existsSync(hashFile)) {
    const expectedHash = fs.readFileSync(hashFile, 'utf8').trim();
    const actualHash = crypto.createHash('sha512').update(bytes).digest('base64');
    if (expectedHash !== actualHash) fail(`SHA-512 mismatch for '${filePath}'.`);
  }
  return { ...result, path: filePath, reason: 'package-id-version-nuspec-and-layout-valid' };
}

function validatePackageBytes(bytes, packageId, packageVersion, rid) {
  const entries = readZipEntries(bytes);
  const nuspecName = entries.find(name => name.toLowerCase().endsWith('.nuspec'));
  if (!nuspecName) fail(`Package '${packageId}' has no nuspec.`);
  const nuspec = readZipEntry(bytes, entries, nuspecName).toString('utf8');
  const actualId = xmlValue(nuspec, 'id');
  const actualVersion = xmlValue(nuspec, 'version');
  if (actualId.toLowerCase() !== packageId.toLowerCase()) fail(`Package id mismatch: '${actualId}'.`);
  if (actualVersion !== packageVersion) fail(`Package version mismatch: '${actualVersion}'.`);
  for (const name of entries) {
    if (name.includes('\\') || name.split('/').includes('..') || name.startsWith('/')) fail(`Unsafe ZIP entry '${name}'.`);
  }
  if (rid) {
    const executable = rid.startsWith('win-') ? `tools/${rid}/so.exe` : `tools/${rid}/so`;
    for (const required of [executable, `tools/${rid}/runtime.json`, 'tools/' + rid + '/docs/en/guides/so-guide.md']) {
      if (!entries.includes(required)) fail(`Self-contained package is missing '${required}'.`);
    }
  }
  return { url: null };
}

function xmlValue(xml, name) {
  const match = xml.match(new RegExp(`<[^>]*${name}\\s*>\\s*([^<]+?)\\s*</[^>]*${name}\\s*>`, 'i'));
  if (!match) fail(`nuspec is missing '${name}'.`);
  return match[1].trim();
}

function readZipEntries(bytes) {
  const end = Math.max(0, bytes.length - 0xffff - 22);
  let eocd = -1;
  for (let index = bytes.length - 22; index >= end; index -= 1) {
    if (bytes.readUInt32LE(index) === 0x06054b50) {
      eocd = index;
      break;
    }
  }
  if (eocd < 0) fail('ZIP end-of-central-directory record was not found.');
  const count = bytes.readUInt16LE(eocd + 10);
  const offset = bytes.readUInt32LE(eocd + 16);
  const names = [];
  let cursor = offset;
  for (let index = 0; index < count; index += 1) {
    if (bytes.readUInt32LE(cursor) !== 0x02014b50) fail('ZIP central directory is invalid.');
    const nameLength = bytes.readUInt16LE(cursor + 28);
    const extraLength = bytes.readUInt16LE(cursor + 30);
    const commentLength = bytes.readUInt16LE(cursor + 32);
    names.push(bytes.subarray(cursor + 46, cursor + 46 + nameLength).toString('utf8'));
    cursor += 46 + nameLength + extraLength + commentLength;
  }
  return names;
}

function readZipEntry(bytes, entries, name) {
  let cursor = 0;
  while (cursor < bytes.length - 30) {
    if (bytes.readUInt32LE(cursor) !== 0x04034b50) {
      cursor += 1;
      continue;
    }
    const nameLength = bytes.readUInt16LE(cursor + 26);
    const extraLength = bytes.readUInt16LE(cursor + 28);
    const entryName = bytes.subarray(cursor + 30, cursor + 30 + nameLength).toString('utf8');
    const compressedSize = bytes.readUInt32LE(cursor + 18);
    const method = bytes.readUInt16LE(cursor + 8);
    const dataStart = cursor + 30 + nameLength + extraLength;
    if (entryName === name) {
      const compressed = bytes.subarray(dataStart, dataStart + compressedSize);
      if (method === 0) return compressed;
      if (method === 8) return zlib.inflateRawSync(compressed);
      fail(`Unsupported ZIP compression method '${method}' for '${name}'.`);
    }
    cursor = dataStart + compressedSize;
  }
  fail(`ZIP entry '${name}' was not found.`);
}

function downloadExact(packageId, packageVersion) {
  const slug = packageId.toLowerCase();
  const exact = packageVersion.toLowerCase();
  const urls = [
    `https://api.nuget.org/v3-flatcontainer/${slug}/${exact}/${slug}.${exact}.nupkg`,
    `https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/${packageId}.${packageVersion}.nupkg`,
  ];
  for (const url of urls) {
    try {
      const bytes = fetchBufferSync(url);
      const sha512Url = `${url}.sha512`;
      const sha512 = fetchTextSync(sha512Url);
      const actual = crypto.createHash('sha512').update(bytes).digest('base64');
      if (sha512 !== actual) {
        throw new Error(`SHA-512 mismatch for downloaded package '${url}'.`);
      }
      return { bytes, url, sha512 };
    } catch {
      // Try only the same exact version on the approved fallback source.
    }
  }
  fail(`Unable to download exact package '${packageId}' at '${packageVersion}'.`);
}

function fetchBufferSync(url) {
  return require('child_process').execFileSync(process.execPath, [
    '-e',
    `fetch(${JSON.stringify(url)}).then(async response => { if (!response.ok) process.exit(2); process.stdout.write(Buffer.from(await response.arrayBuffer())); }).catch(() => process.exit(2));`,
  ], { maxBuffer: 600 * 1024 * 1024 });
}

function fetchTextSync(url) {
  return require('child_process').execFileSync(process.execPath, [
    '-e',
    `fetch(${JSON.stringify(url)}).then(async response => { if (!response.ok) process.exit(2); process.stdout.write(await response.text()); }).catch(() => process.exit(2));`,
  ], { encoding: 'utf8', maxBuffer: 1024 * 1024 }).trim();
}
function writeResult(value) {
  process.stdout.write(`${JSON.stringify(value)}\n`);
}

function fail(message) {
  process.stderr.write(`${message}\n`);
  process.exit(2);
}
