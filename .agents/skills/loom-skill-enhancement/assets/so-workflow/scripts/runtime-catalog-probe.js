#!/usr/bin/env node
'use strict';

const crypto = require('crypto');
const fs = require('fs');
const path = require('path');
const { spawnSync } = require('child_process');
const common = require('./migration-common');

const reportSchema = 'loom-skill-enhancement-runtime-catalog-probe.v1';
const providerLimits = Object.freeze({
  max_bytes: 32768,
  max_array_items: 256,
  max_object_properties: 256,
  max_depth: 6,
});
const usage = `Usage: node runtime-catalog-probe.js --runtime-apphost <so.exe> --package-validation-file <runtime-package-validation.json> --contract-file <owning-contract.json> --target-skill-root <directory> --report-file <new-external-report.json>

Runs an exact-runtime /context_fields read and boundary probes. Raw runtime outputs and probe contracts are kept beside the report.`;

function samePath(left, right) {
  const first = path.resolve(left);
  const second = path.resolve(right);
  return process.platform === 'win32'
    ? first.toLowerCase() === second.toLowerCase()
    : first === second;
}

function isWithin(root, candidate) {
  const relative = path.relative(path.resolve(root), path.resolve(candidate));
  return relative === ''
    || (relative !== '..' && !relative.startsWith(`..${path.sep}`) && !path.isAbsolute(relative));
}

function parseRuntimeOutput(text) {
  const trimmed = text.trim();
  const wrapper = trimmed.match(/^<so_property>\s*([\s\S]*?)\s*<\/so_property>$/i);
  const candidate = wrapper ? wrapper[1] : trimmed;
  try {
    return JSON.parse(candidate);
  } catch {
    return null;
  }
}

function payloadMessage(payload, stdout, stderr) {
  if (common.isRecord(payload?.payload) && typeof payload.payload.message === 'string') return payload.payload.message;
  if (typeof payload?.message === 'string') return payload.message;
  if (common.isRecord(payload)) return '';
  return `${stdout}${stderr}`.trim().slice(0, 512);
}

function expectedLimitsMatch(value) {
  return common.isRecord(value)
    && value.maxBytes === providerLimits.max_bytes
    && value.maxArrayItems === providerLimits.max_array_items
    && value.maxObjectProperties === providerLimits.max_object_properties
    && value.maxDepth === providerLimits.max_depth;
}

function writeProbeContract(directory, name, contextFields) {
  const filePath = path.join(directory, `${name}.contract.json`);
  const contract = {
    name: 'runtime-catalog-boundary-probe',
    inputs: {},
    outputs: {},
    default_assumptions: {},
    context_fields: contextFields,
  };
  fs.writeFileSync(filePath, JSON.stringify(contract), { encoding: 'utf8', flag: 'wx' });
  return filePath;
}

function baseField(pathValue) {
  return {
    path: pathValue,
    type: 'string',
    nullable: false,
    owner: 'agent',
    description: 'Boundary probe.',
  };
}

function makeExactByteContract(directory, name, targetBytes) {
  const field = baseField('probe_value');
  const contextFields = { schemaVersion: 'loom.context-fields.v1', fields: [field] };
  const before = Buffer.byteLength(JSON.stringify(contextFields), 'utf8');
  const descriptionLength = targetBytes - before + Buffer.byteLength(field.description, 'utf8');
  if (!Number.isSafeInteger(descriptionLength) || descriptionLength < 0) {
    common.fail(`Cannot construct a ${targetBytes}-byte context_fields probe.`);
  }
  field.description = 'x'.repeat(descriptionLength);
  const actualBytes = Buffer.byteLength(JSON.stringify(contextFields), 'utf8');
  if (actualBytes !== targetBytes) common.fail(`Expected a ${targetBytes}-byte context_fields probe, constructed ${actualBytes} bytes.`);
  return { path: writeProbeContract(directory, name, contextFields), fragmentBytes: actualBytes };
}

function makeArrayContract(directory, name, itemCount) {
  const fields = Array.from({ length: itemCount }, (_, index) => baseField(`probe_${index}`));
  const contextFields = { schemaVersion: 'loom.context-fields.v1', fields };
  const fragmentBytes = Buffer.byteLength(JSON.stringify(contextFields), 'utf8');
  if (fragmentBytes > providerLimits.max_bytes) common.fail(`${itemCount}-field probe exceeds the byte limit before the array boundary is tested.`);
  return { path: writeProbeContract(directory, name, contextFields), fragmentBytes };
}

function makeObjectContract(directory, name, propertyCount) {
  const contextFields = { schemaVersion: 'loom.context-fields.v1', fields: [] };
  const extraCount = propertyCount - Object.keys(contextFields).length;
  if (extraCount < 0) common.fail(`Object property count ${propertyCount} is smaller than the required probe shape.`);
  for (let index = 0; index < extraCount; index += 1) contextFields[`probe_${index}`] = null;
  const fragmentBytes = Buffer.byteLength(JSON.stringify(contextFields), 'utf8');
  return { path: writeProbeContract(directory, name, contextFields), fragmentBytes };
}

function makeDepthContract(directory, name, leafDepth) {
  let nested = 'leaf';
  for (let depth = 1; depth < leafDepth; depth += 1) nested = { next: nested };
  const contextFields = { schemaVersion: 'loom.context-fields.v1', fields: [], probe: nested };
  const fragmentBytes = Buffer.byteLength(JSON.stringify(contextFields), 'utf8');
  return { path: writeProbeContract(directory, name, contextFields), fragmentBytes };
}

function main() {
  const args = common.parseArgs(process.argv.slice(2), ['--help']);
  common.assertKnownArgs(args, [
    '--runtime-apphost',
    '--package-validation-file',
    '--contract-file',
    '--target-skill-root',
    '--report-file',
    '--help',
  ]);
  if (args['--help']) {
    process.stdout.write(`${usage}\n`);
    return 0;
  }

  const apphostPath = common.resolveExistingFile(common.requireOption(args, '--runtime-apphost'), 'Runtime apphost');
  const validationPath = common.resolveExistingFile(common.requireOption(args, '--package-validation-file'), 'Runtime package validation report');
  const contractPath = common.resolveExistingFile(common.requireOption(args, '--contract-file'), 'Owning contract');
  const targetRoot = path.resolve(common.requireOption(args, '--target-skill-root'));
  if (!fs.existsSync(targetRoot) || !fs.statSync(targetRoot).isDirectory()) common.fail(`Target skill root '${targetRoot}' does not exist as a directory.`);
  const reportPath = common.resolveDestination(common.requireOption(args, '--report-file'), 'Runtime probe report');
  const evidenceDirectory = `${reportPath.slice(0, reportPath.length - path.extname(reportPath).length)}-evidence`;
  if (fs.existsSync(evidenceDirectory)) common.fail(`Runtime probe evidence directory '${evidenceDirectory}' already exists.`);
  if (isWithin(targetRoot, reportPath) || isWithin(targetRoot, evidenceDirectory)) {
    common.fail('Runtime probe reports and evidence must be outside the target skill root.');
  }
  common.assertDistinctPaths([
    ['runtime apphost', apphostPath],
    ['package validation report', validationPath],
    ['owning contract', contractPath],
    ['runtime probe report', reportPath],
    ['runtime probe evidence directory', evidenceDirectory],
  ]);

  const { value: validation } = common.readJsonFile(validationPath, 'Runtime package validation report');
  if (!common.isRecord(validation)
    || validation.source !== 'nuget-registration'
    || validation.package_integrity !== 'passed'
    || validation.archive_and_manifest_validation !== 'passed'
    || validation.documentation_extraction_and_hash_validation !== 'passed'
    || typeof validation.version !== 'string'
    || typeof validation.rid !== 'string'
    || validation.package_id !== `Techne.Loom.SkillOrchestrator.Runtime.${validation.rid}`
    || !Number.isSafeInteger(validation.archive_bytes)
    || typeof validation.package_sha512 !== 'string') {
    common.fail('Runtime package validation report does not prove a verified self-contained NuGet runtime package.');
  }

  const packagePath = common.resolveExistingFile(validation.package_path, 'Published runtime package');
  const validatedApphostPath = common.resolveExistingFile(validation.apphost_path, 'Validated runtime apphost');
  const manifestPath = common.resolveExistingFile(validation.manifest_path, 'Runtime manifest');
  const guidePath = common.resolveExistingFile(validation.guide_path, 'Runtime guide');
  if (!samePath(apphostPath, validatedApphostPath)) common.fail('Selected runtime apphost differs from the apphost in the validation report.');
  const packageSha512 = crypto.createHash('sha512').update(fs.readFileSync(packagePath)).digest('base64');
  if (packageSha512 !== validation.package_sha512 || fs.statSync(packagePath).size !== validation.archive_bytes) {
    common.fail('Published runtime package bytes no longer match the validated package SHA-512 and archive size.');
  }
  const apphostSha256 = common.hashFile(apphostPath);
  const manifestSha256 = common.hashFile(manifestPath);
  const guideSha256 = common.hashFile(guidePath);
  const validationSha256 = common.hashFile(validationPath);
  const contractSha256 = common.hashFile(contractPath);
  const { value: contract } = common.readJsonFile(contractPath, 'Owning contract');
  if (!common.isRecord(contract?.context_fields) || !Array.isArray(contract.context_fields.fields)) {
    common.fail('Owning contract must contain context_fields.fields before the runtime probe can run.');
  }

  fs.mkdirSync(evidenceDirectory, { recursive: true });
  const inputDirectory = path.join(evidenceDirectory, 'probe-contracts');
  const outputDirectory = path.join(evidenceDirectory, 'runtime-responses');
  fs.mkdirSync(inputDirectory);
  fs.mkdirSync(outputDirectory);
  const invocations = [];
  const blockers = [];

  function invoke(name, sourceContractPath, commandArgs = null) {
    const invocationArgs = commandArgs ?? [
      'inspect-contract-fragment',
      '--contract-file', sourceContractPath,
      '--json-pointer', '/context_fields',
      '--max-bytes', String(providerLimits.max_bytes),
      '--max-array-items', String(providerLimits.max_array_items),
      '--max-object-properties', String(providerLimits.max_object_properties),
      '--max-depth', String(providerLimits.max_depth),
    ];
    const result = spawnSync(apphostPath, invocationArgs, { encoding: 'utf8', windowsHide: true });
    const stdout = result.stdout ?? '';
    const stderr = result.stderr ?? '';
    const stdoutPath = path.join(outputDirectory, `${name}.stdout.txt`);
    const stderrPath = path.join(outputDirectory, `${name}.stderr.txt`);
    fs.writeFileSync(stdoutPath, stdout, { encoding: 'utf8', flag: 'wx' });
    fs.writeFileSync(stderrPath, stderr, { encoding: 'utf8', flag: 'wx' });
    const payload = parseRuntimeOutput(stdout);
    const invocation = {
      name,
      contract_path: sourceContractPath,
      contract_sha256: sourceContractPath ? common.hashFile(sourceContractPath) : null,
      exit_code: result.status,
      process_error: result.error?.message ?? null,
      stdout: { path: stdoutPath, sha256: common.hashFile(stdoutPath), bytes: fs.statSync(stdoutPath).size },
      stderr: { path: stderrPath, sha256: common.hashFile(stderrPath), bytes: fs.statSync(stderrPath).size },
      payload,
      message: payloadMessage(payload, stdout, stderr),
    };
    invocations.push(invocation);
    return invocation;
  }

  function expectSuccess(name, inputPath, expectedBytes) {
    const invocation = invoke(name, inputPath);
    const payload = invocation.payload;
    const passed = invocation.exit_code === 0
      && common.isRecord(payload)
      && samePath(payload.contractPath, inputPath)
      && payload.contractSha256 === invocation.contract_sha256
      && payload.returnedBytes === expectedBytes
      && expectedLimitsMatch(payload.limits);
    if (!passed) {
      blockers.push({ code: 'runtime_probe_success_mismatch', probe: name, message: invocation.message });
    }
    return {
      status: passed ? 'passed' : 'failed',
      expected_bytes: expectedBytes,
      actual_bytes: Number.isSafeInteger(payload?.returnedBytes) ? payload.returnedBytes : null,
      limits: common.isRecord(payload?.limits) ? payload.limits : null,
      invocation: name,
    };
  }

  function expectFailure(name, inputPath, expectedMessage) {
    const invocation = invoke(name, inputPath);
    const passed = invocation.exit_code !== 0 && invocation.message.includes(expectedMessage);
    if (!passed) blockers.push({ code: 'runtime_probe_boundary_mismatch', probe: name, message: invocation.message });
    return {
      status: passed ? 'passed' : 'failed',
      expected_message: expectedMessage,
      actual_message: invocation.message,
      invocation: name,
    };
  }

  const guideInvocation = invoke('runtime-guide', null, ['--guide']);
  const guidePassed = guideInvocation.exit_code === 0
    && common.isRecord(guideInvocation.payload)
    && guideInvocation.payload.version === validation.version
    && samePath(guideInvocation.payload.guide_path, guidePath);
  if (!guidePassed) blockers.push({ code: 'runtime_guide_identity_mismatch', message: guideInvocation.message });

  const currentResponse = invoke('current-catalog', contractPath);
  const currentPayload = currentResponse.payload;
  const currentPassed = currentResponse.exit_code === 0
    && common.isRecord(currentPayload)
    && samePath(currentPayload.contractPath, contractPath)
    && currentPayload.contractSha256 === contractSha256
    && Number.isSafeInteger(currentPayload.returnedBytes)
    && expectedLimitsMatch(currentPayload.limits);
  if (!currentPassed) blockers.push({ code: 'runtime_catalog_read_failed', message: currentResponse.message });
  if (currentPassed && currentPayload.returnedBytes > providerLimits.max_bytes) {
    blockers.push({ code: 'runtime_catalog_bytes_exceed_limit', returned_bytes: currentPayload.returnedBytes });
  }

  const byteExact = makeExactByteContract(inputDirectory, 'bytes-exact-limit', providerLimits.max_bytes);
  const byteOver = makeExactByteContract(inputDirectory, 'bytes-over-limit', providerLimits.max_bytes + 1);
  const arrayExact = makeArrayContract(inputDirectory, 'array-exact-limit', providerLimits.max_array_items);
  const arrayOver = makeArrayContract(inputDirectory, 'array-over-limit', providerLimits.max_array_items + 1);
  const objectExact = makeObjectContract(inputDirectory, 'object-exact-limit', providerLimits.max_object_properties);
  const objectOver = makeObjectContract(inputDirectory, 'object-over-limit', providerLimits.max_object_properties + 1);
  const depthExact = makeDepthContract(inputDirectory, 'depth-exact-limit', providerLimits.max_depth);
  const depthOver = makeDepthContract(inputDirectory, 'depth-over-limit', providerLimits.max_depth + 1);

  const boundaries = {
    bytes_at_limit: expectSuccess('bytes-exact-limit', byteExact.path, byteExact.fragmentBytes),
    bytes_over_limit: expectFailure('bytes-over-limit', byteOver.path, `exceeds max bytes ${providerLimits.max_bytes}`),
    array_at_limit: { ...expectSuccess('array-exact-limit', arrayExact.path, arrayExact.fragmentBytes), expected_items: providerLimits.max_array_items },
    array_over_limit: { ...expectFailure('array-over-limit', arrayOver.path, `exceeds max array items ${providerLimits.max_array_items}`), attempted_items: providerLimits.max_array_items + 1 },
    object_properties_at_limit: { ...expectSuccess('object-exact-limit', objectExact.path, objectExact.fragmentBytes), expected_properties: providerLimits.max_object_properties },
    object_properties_over_limit: { ...expectFailure('object-over-limit', objectOver.path, `exceeds max object properties ${providerLimits.max_object_properties}`), attempted_properties: providerLimits.max_object_properties + 1 },
    depth_at_limit: { ...expectSuccess('depth-exact-limit', depthExact.path, depthExact.fragmentBytes), expected_leaf_depth: providerLimits.max_depth },
    depth_over_limit: { ...expectFailure('depth-over-limit', depthOver.path, `exceeds max depth ${providerLimits.max_depth}`), attempted_leaf_depth: providerLimits.max_depth + 1 },
  };

  const report = {
    schema_version: reportSchema,
    status: blockers.length === 0 ? 'passed' : 'blocked',
    blockers,
    probe_tool: {
      path: path.resolve(__filename),
      sha256: common.hashFile(__filename),
    },
    runtime: {
      package_id: validation.package_id,
      version: validation.version,
      rid: validation.rid,
      package_path: packagePath,
      package_sha512: packageSha512,
      package_validation_path: validationPath,
      package_validation_sha256: validationSha256,
      apphost_path: apphostPath,
      apphost_sha256: apphostSha256,
      manifest_path: manifestPath,
      manifest_sha256: manifestSha256,
      guide_path: guidePath,
      guide_sha256: guideSha256,
      guide_invocation: 'runtime-guide',
    },
    catalog: {
      contract_path: contractPath,
      contract_sha256: contractSha256,
      json_pointer: '/context_fields',
      returned_bytes: Number.isSafeInteger(currentPayload?.returnedBytes) ? currentPayload.returnedBytes : null,
      limits: common.isRecord(currentPayload?.limits) ? currentPayload.limits : null,
      invocation: 'current-catalog',
    },
    provider_limit_profile: providerLimits,
    boundaries,
    invocations: invocations.map(invocation => ({
      name: invocation.name,
      contract_path: invocation.contract_path,
      contract_sha256: invocation.contract_sha256,
      exit_code: invocation.exit_code,
      process_error: invocation.process_error,
      stdout: invocation.stdout,
      stderr: invocation.stderr,
      message: invocation.message,
    })),
  };
  common.writeJsonNoOverwrite(reportPath, report);
  process.stdout.write(`${JSON.stringify({
    status: report.status,
    report: reportPath,
    report_sha256: common.hashFile(reportPath),
    returned_bytes: report.catalog.returned_bytes,
    boundary_count: Object.keys(boundaries).length,
    blockers: blockers.length,
  })}\n`);
  return blockers.length === 0 ? 0 : 2;
}

common.finish(main);
