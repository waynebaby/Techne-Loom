#!/usr/bin/env node
'use strict';

const assert = require('assert');
const crypto = require('crypto');
const fs = require('fs');
const os = require('os');
const path = require('path');
const { spawnSync } = require('child_process');

const script = path.join(__dirname, 'preflight-target-skill.js');
let checks = 0;

function check(condition, message) {
  assert.ok(condition, message);
  checks += 1;
}

function equal(actual, expected, message) {
  assert.strictEqual(actual, expected, message);
  checks += 1;
}

function json(filePath, value) {
  fs.mkdirSync(path.dirname(filePath), { recursive: true });
  fs.writeFileSync(filePath, `${JSON.stringify(value, null, 2)}\n`, 'utf8');
}

function hash(filePath) {
  return crypto.createHash('sha256').update(fs.readFileSync(filePath)).digest('hex');
}

function git(repository, args) {
  const result = spawnSync('git', ['-C', repository, ...args], { encoding: 'utf8', windowsHide: true });
  if (result.error) throw result.error;
  assert.strictEqual(result.status, 0, `git ${args.join(' ')} failed: ${result.stderr}`);
}

function run(input, reportPath, expectedStatus = 0) {
  const inputPath = path.join(path.dirname(reportPath), `${path.basename(reportPath, '.json')}.input.json`);
  json(inputPath, input);
  const result = spawnSync(process.execPath, [script, '--input-file', inputPath, '--report-file', reportPath], {
    encoding: 'utf8',
    windowsHide: true,
  });
  if (result.error) throw result.error;
  const report = fs.existsSync(reportPath) ? JSON.parse(fs.readFileSync(reportPath, 'utf8')) : null;
  const diagnostics = report ? JSON.stringify(report.blockers) : `${result.stdout}${result.stderr}`;
  equal(result.status, expectedStatus, `preflight expected exit ${expectedStatus}, got ${result.status}: ${diagnostics}`);
  return { report, stdout: result.stdout, stderr: result.stderr, inputPath };
}

function metadata(pathValue, type, owner, nullable = false, extra = {}) {
  return { path: pathValue, type, nullable, owner, description: `Fixture declaration for ${pathValue}.`, ...extra };
}

function makeFixture() {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'loom-preflight-fixture-'));
  const repository = path.join(root, 'repo');
  const target = path.join(repository, 'skills', 'fixture-skill');
  const evidence = path.join(root, 'evidence');
  const runtimeGuide = path.join(evidence, 'so-guide.md');
  const contextFile = path.join(evidence, 'context.json');
  fs.mkdirSync(target, { recursive: true });
  fs.mkdirSync(evidence, { recursive: true });

  const fields = [
    metadata('approval', 'string', 'user'),
    metadata('answer', 'string', 'user'),
    metadata('result', 'string', 'agent'),
    metadata('runtime_result', 'string', 'runtime', true),
  ];
  const workflow = {
    instanceId: 'fixture-template',
    templateKind: 'so-governed-target-skill',
    taskType: 'skill_enhancement',
    workflowKind: 'target_skill_enhancement',
    caseId: 'fixture-case',
    runId: 'template:fixture',
    runtimeBinding: 'dotnet-so',
    validation: {
      gates: {
        'gate.done': {
          passExpression: {
            kind: 'predicate',
            source: 'context.Get<string>("result") != null',
            entryPoint: 'Evaluate',
            resultType: 'bool',
          },
          requiredOutputFamilies: ['result'],
          valueSemantics: { result: 'nonEmptyString' },
        },
      },
      declaredUserOwnedFields: ['approval'],
      reservedRuntimeOwnedFields: ['runtime_result'],
    },
    nodes: {
      'state.start': { $kind: 'state', groups: [{ id: 'group.start', transitionIds: ['transition.memory', 'transition.wait'] }] },
      'transition.memory': {
        $kind: 'command',
        stepKind: 'memoryRead',
        targetNodeId: 'state.done',
        command: { kind: 'nativeCode', name: 'memory.read', parameters: { filePaths: ['assets/so-workflow/reference/notes.md'] } },
      },
      'transition.wait': {
        $kind: 'command',
        stepKind: 'waitResume',
        targetNodeId: 'state.done',
        outputPath: 'result',
        command: {
          kind: 'nativeCode',
          name: 'workflow.wait',
          parameters: {
            requiredInputs: ['answer', 'runtime_result'],
            resumeOutputKey: 'answer',
            outputBindings: { result: '$payload:answer', approval: '$context:approval' },
          },
        },
      },
      'state.done': { $kind: 'state', groups: [] },
    },
  };
  const contract = {
    name: 'fixture target skill',
    inputs: { answer: { type: 'string' } },
    outputs: { result: { type: 'string' } },
    default_assumptions: {},
  };
  const catalog = {
    name: 'fixture owning context catalog',
    inputs: {},
    outputs: {},
    default_assumptions: {},
    context_fields: { schemaVersion: 'loom.context-fields.v1', fields },
  };
  const files = {
    'SKILL.md': '# Fixture skill\n',
    'assets/so-workflow/contract.json': `${JSON.stringify(contract, null, 2)}\n`,
    'assets/so-workflow/workflow.json': `${JSON.stringify(workflow, null, 2)}\n`,
    'assets/so-workflow/owning-context-catalog.json': `${JSON.stringify(catalog, null, 2)}\n`,
    'assets/so-workflow/node-to-file-map.md': '| state.start | SKILL.md |\n',
    'assets/so-workflow/reference/document-copy-manifest.json': `${JSON.stringify({ schema_version: 'fixture.v1', entries: [] }, null, 2)}\n`,
    'assets/so-workflow/reference/notes.md': '# Local read-only reference\n',
  };
  for (const [relative, content] of Object.entries(files)) {
    const absolute = path.join(target, ...relative.split('/'));
    fs.mkdirSync(path.dirname(absolute), { recursive: true });
    fs.writeFileSync(absolute, content, 'utf8');
  }
  fs.writeFileSync(runtimeGuide, '# Fixture SO guide\n', 'utf8');
  json(contextFile, { runtime_result: null });

  git(repository, ['init', '--quiet']);
  git(repository, ['add', '--all']);
  git(repository, ['-c', 'user.name=Fixture', '-c', 'user.email=fixture@example.test', 'commit', '--quiet', '-m', 'fixture baseline']);

  const assetInventory = Object.keys(files).map(relative => ({
    path: relative,
    classification: 'required',
    reason: `Fixture source required for ${relative}.`,
    derived_from: relative.includes('contract.json')
      ? ['business_contract']
      : relative.includes('workflow.json')
        ? ['workflow', 'selected_route']
        : relative.includes('node-to-file-map')
          ? ['node_to_file_map']
          : relative.includes('document-copy-manifest')
            ? ['document_copy_manifest']
            : relative.includes('notes.md')
              ? ['workflow', 'node_to_file_map']
              : ['business_contract'],
    expected_sha256: hash(path.join(target, ...relative.split('/'))),
  }));

  return {
    root,
    repository,
    target,
    evidence,
    runtimeGuide,
    contextFile,
    workflowPath: path.join(target, 'assets', 'so-workflow', 'workflow.json'),
    catalogPath: path.join(target, 'assets', 'so-workflow', 'owning-context-catalog.json'),
    workflow,
    catalog,
    contract,
    fields,
    assetInventory,
  };
}

function baselineInput(fixture, overrides = {}) {
  return {
    schema_version: 'loom-skill-enhancement-preflight.v1',
    phase: 'baseline',
    repository_root: fixture.repository,
    target_skill_root: fixture.target,
    requested_ref: null,
    ...overrides,
  };
}

function writeRuntimeEvidence(fixture) {
  if (fixture.runtimeEvidenceReady) return;
  const limits = { maxBytes: 32768, maxArrayItems: 256, maxObjectProperties: 256, maxDepth: 6 };
  const profile = { max_bytes: 32768, max_array_items: 256, max_object_properties: 256, max_depth: 6 };
  const evidenceRoot = path.join(fixture.evidence, 'runtime-probe-evidence');
  const contractsDirectory = path.join(evidenceRoot, 'probe-contracts');
  const responsesDirectory = path.join(evidenceRoot, 'runtime-responses');
  fs.mkdirSync(contractsDirectory, { recursive: true });
  fs.mkdirSync(responsesDirectory, { recursive: true });

  fixture.runtimePackagePath = path.join(fixture.evidence, 'fixture-runtime.nupkg');
  fixture.runtimeApphostPath = path.join(fixture.evidence, 'fixture-so.exe');
  fixture.runtimeManifestPath = path.join(fixture.evidence, 'fixture-runtime-manifest.json');
  fixture.runtimePackageValidationPath = path.join(fixture.evidence, 'fixture-package-validation.json');
  fixture.runtimeProbePath = path.join(fixture.evidence, 'fixture-runtime-catalog-probe.json');
  fixture.runtimeProbeToolPath = path.join(__dirname, 'runtime-catalog-probe.js');
  fs.writeFileSync(fixture.runtimePackagePath, 'fixture package bytes', 'utf8');
  fs.writeFileSync(fixture.runtimeApphostPath, 'fixture runtime apphost bytes', 'utf8');
  json(fixture.runtimeManifestPath, { fixture: true, runtime: 'fixture-runtime' });

  const packageId = 'Techne.Loom.SkillOrchestrator.Runtime.fixture-win-x64';
  const version = 'fixture-runtime';
  const rid = 'fixture-win-x64';
  const packageSha512 = crypto.createHash('sha512').update(fs.readFileSync(fixture.runtimePackagePath)).digest('base64');
  const packageValidation = {
    source: 'nuget-registration',
    package_integrity: 'passed',
    archive_and_manifest_validation: 'passed',
    documentation_extraction_and_hash_validation: 'passed',
    package_id: packageId,
    version,
    rid,
    package_path: fixture.runtimePackagePath,
    package_sha512: packageSha512,
    archive_bytes: fs.statSync(fixture.runtimePackagePath).size,
    apphost_path: fixture.runtimeApphostPath,
    manifest_path: fixture.runtimeManifestPath,
    guide_path: fixture.runtimeGuide,
  };
  json(fixture.runtimePackageValidationPath, packageValidation);

  const catalog = JSON.parse(fs.readFileSync(fixture.catalogPath, 'utf8'));
  const catalogBytes = Buffer.byteLength(JSON.stringify(catalog.context_fields), 'utf8');
  const invocations = [];
  const boundaries = {};

  function addInvocation(name, contractPath, payload, exitCode = 0) {
    const stdoutPath = path.join(responsesDirectory, `${name}.stdout.txt`);
    const stderrPath = path.join(responsesDirectory, `${name}.stderr.txt`);
    const stdout = JSON.stringify(payload);
    fs.writeFileSync(stdoutPath, stdout, 'utf8');
    fs.writeFileSync(stderrPath, '', 'utf8');
    const invocation = {
      name,
      contract_path: contractPath,
      contract_sha256: contractPath ? hash(contractPath) : null,
      exit_code: exitCode,
      process_error: null,
      stdout: { path: stdoutPath, sha256: hash(stdoutPath), bytes: fs.statSync(stdoutPath).size },
      stderr: { path: stderrPath, sha256: hash(stderrPath), bytes: fs.statSync(stderrPath).size },
      message: typeof payload?.message === 'string' ? payload.message : '',
    };
    invocations.push(invocation);
    return invocation;
  }

  function writeBoundaryContract(name, contextFields) {
    const contractPath = path.join(contractsDirectory, `${name}.contract.json`);
    const contract = {
      name: 'runtime-catalog-boundary-probe-fixture',
      inputs: {},
      outputs: {},
      default_assumptions: {},
      context_fields: contextFields,
    };
    const compact = JSON.stringify(contract);
    fs.writeFileSync(contractPath, compact, 'utf8');
    return {
      path: contractPath,
      sha256: hash(contractPath),
      bytes: Buffer.byteLength(JSON.stringify(contextFields), 'utf8'),
    };
  }

  function responseFor(contract, contextFields) {
    return {
      contractPath: contract.path,
      contractSha256: contract.sha256,
      returnedBytes: contract.bytes,
      limits,
      fragments: { '/context_fields': contextFields },
    };
  }

  function acceptedBoundary(name, key, contextFields, extra = {}) {
    const invocationName = `${name}-exact-limit`;
    const contract = writeBoundaryContract(invocationName, contextFields);
    addInvocation(invocationName, contract.path, responseFor(contract, contextFields));
    boundaries[key] = {
      status: 'passed',
      expected_bytes: contract.bytes,
      actual_bytes: contract.bytes,
      limits,
      invocation: invocationName,
      ...extra,
    };
  }

  function rejectedBoundary(name, key, contextFields, dimension, limit, extra = {}) {
    const invocationName = `${name}-over-limit`;
    const contract = writeBoundaryContract(invocationName, contextFields);
    const expectedMessage = `exceeds max ${dimension} ${limit}`;
    addInvocation(invocationName, contract.path, { message: expectedMessage }, 2);
    boundaries[key] = {
      status: 'passed',
      expected_message: expectedMessage,
      actual_message: expectedMessage,
      invocation: invocationName,
      ...extra,
    };
  }

  function byteBoundary(targetBytes) {
    const contextFields = { schemaVersion: 'loom.context-fields.v1', fields: [], padding: '' };
    const fixedBytes = Buffer.byteLength(JSON.stringify(contextFields), 'utf8');
    contextFields.padding = 'x'.repeat(targetBytes - fixedBytes);
    if (Buffer.byteLength(JSON.stringify(contextFields), 'utf8') !== targetBytes) throw new Error('Fixture byte boundary is not exact.');
    return contextFields;
  }

  function depthBoundary(leafDepth) {
    let nested = 'leaf';
    for (let depth = 1; depth < leafDepth; depth += 1) nested = { next: nested };
    return { schemaVersion: 'loom.context-fields.v1', fields: [], probe: nested };
  }

  addInvocation('runtime-guide', null, { version, guide_path: fixture.runtimeGuide });
  addInvocation('current-catalog', fixture.catalogPath, {
    contractPath: fixture.catalogPath,
    contractSha256: hash(fixture.catalogPath),
    returnedBytes: catalogBytes,
    limits,
    fragments: { '/context_fields': catalog.context_fields },
  });

  acceptedBoundary('bytes', 'bytes_at_limit', byteBoundary(profile.max_bytes));
  rejectedBoundary('bytes', 'bytes_over_limit', byteBoundary(profile.max_bytes + 1), 'bytes', profile.max_bytes);
  const arrayAtLimit = Array.from({ length: profile.max_array_items }, (_, index) => ({ path: `field_${index}` }));
  const arrayOverLimit = Array.from({ length: profile.max_array_items + 1 }, (_, index) => ({ path: `field_${index}` }));
  acceptedBoundary('array', 'array_at_limit', { schemaVersion: 'loom.context-fields.v1', fields: arrayAtLimit }, { expected_items: profile.max_array_items });
  rejectedBoundary('array', 'array_over_limit', { schemaVersion: 'loom.context-fields.v1', fields: arrayOverLimit }, 'array items', profile.max_array_items, { attempted_items: profile.max_array_items + 1 });
  const objectAtLimit = { schemaVersion: 'loom.context-fields.v1', fields: [] };
  for (let index = 0; index < profile.max_object_properties - 2; index += 1) objectAtLimit[`property_${index}`] = 'value';
  const objectOverLimit = { ...objectAtLimit, [`property_${profile.max_object_properties - 2}`]: 'value' };
  acceptedBoundary('object', 'object_properties_at_limit', objectAtLimit, { expected_properties: profile.max_object_properties });
  rejectedBoundary('object', 'object_properties_over_limit', objectOverLimit, 'object properties', profile.max_object_properties, { attempted_properties: profile.max_object_properties + 1 });
  acceptedBoundary('depth', 'depth_at_limit', depthBoundary(profile.max_depth), { expected_leaf_depth: profile.max_depth });
  rejectedBoundary('depth', 'depth_over_limit', depthBoundary(profile.max_depth + 1), 'depth', profile.max_depth, { attempted_leaf_depth: profile.max_depth + 1 });

  json(fixture.runtimeProbePath, {
    schema_version: 'loom-skill-enhancement-runtime-catalog-probe.v1',
    status: 'passed',
    blockers: [],
    probe_tool: { path: fixture.runtimeProbeToolPath, sha256: hash(fixture.runtimeProbeToolPath) },
    runtime: {
      package_id: packageId,
      version,
      rid,
      package_path: fixture.runtimePackagePath,
      package_sha512: packageSha512,
      package_validation_path: fixture.runtimePackageValidationPath,
      package_validation_sha256: hash(fixture.runtimePackageValidationPath),
      apphost_path: fixture.runtimeApphostPath,
      apphost_sha256: hash(fixture.runtimeApphostPath),
      manifest_path: fixture.runtimeManifestPath,
      manifest_sha256: hash(fixture.runtimeManifestPath),
      guide_path: fixture.runtimeGuide,
      guide_sha256: hash(fixture.runtimeGuide),
      guide_invocation: 'runtime-guide',
    },
    catalog: {
      contract_path: fixture.catalogPath,
      contract_sha256: hash(fixture.catalogPath),
      json_pointer: '/context_fields',
      returned_bytes: catalogBytes,
      limits,
      invocation: 'current-catalog',
    },
    provider_limit_profile: profile,
    boundaries,
    invocations,
  });
  fixture.runtimeEvidenceReady = true;
}

function designInput(fixture, baselinePath, overrides = {}) {
  writeRuntimeEvidence(fixture);
  const sourceSpecs = {
    business_contract: { base: 'target_skill', path: 'assets/so-workflow/contract.json' },
    workflow: { base: 'target_skill', path: 'assets/so-workflow/workflow.json' },
    node_to_file_map: { base: 'target_skill', path: 'assets/so-workflow/node-to-file-map.md' },
    document_copy_manifest: { base: 'target_skill', path: 'assets/so-workflow/reference/document-copy-manifest.json' },
    owning_context_catalog: { base: 'target_skill', path: 'assets/so-workflow/owning-context-catalog.json' },
    context_file: { base: 'external', path: fixture.contextFile },
    runtime_guide: { base: 'external', path: fixture.runtimeGuide },
    runtime_package_validation: { base: 'external', path: fixture.runtimePackageValidationPath },
    runtime_probe: { base: 'external', path: fixture.runtimeProbePath },
    runtime_probe_tool: { base: 'external', path: fixture.runtimeProbeToolPath },
  };
  const resolved = Object.fromEntries(Object.entries(sourceSpecs).map(([key, spec]) => {
    const absolute = spec.base === 'target_skill' ? path.join(fixture.target, ...spec.path.split('/')) : spec.path;
    return [key, { ...spec, sha256: hash(absolute) }];
  }));
  const workflowHash = resolved.workflow.sha256;
  const fields = fixture.fields.map(field => ({
    metadata: field,
    evidence: field.path === 'runtime_result'
      ? [{ source_file: 'context_file', sha256: resolved.context_file.sha256, locator: '/runtime_result' }]
      : field.path === 'answer'
        ? [{ source_file: 'business_contract', sha256: resolved.business_contract.sha256, locator: '/inputs/answer' }]
        : [{ source_file: 'workflow', sha256: workflowHash, locator: field.path === 'approval' ? '/validation/declaredUserOwnedFields/0' : '/validation/gates/gate.done/passExpression/source' }],
  }));
  return {
    schema_version: 'loom-skill-enhancement-preflight.v1',
    phase: 'design',
    repository_root: fixture.repository,
    target_skill_root: fixture.target,
    requested_ref: null,
    baseline_report: { path: baselinePath, sha256: hash(baselinePath) },
    selected_route: 'target_enhancement',
    source_files: sourceSpecs,
    asset_inventory: fixture.assetInventory,
    authoritative_fields: fields,
    required_input_sources: [
      { transition_id: 'transition.wait', path: 'answer', namespace: 'payload', reason: 'External result payload field.' },
      { transition_id: 'transition.wait', path: 'runtime_result', namespace: 'context', reason: 'Existing runtime-owned context value.' },
    ],
    memory_read_inputs: [
      { transition_id: 'transition.memory', parameter_pointer: '/filePaths', reason: 'Local read-only target reference.' },
    ],
    ...overrides,
  };
}

function captureBaseline(fixture, name = 'baseline.json', overrides = {}) {
  const reportPath = path.join(fixture.evidence, name);
  const result = run(baselineInput(fixture, overrides), reportPath, 0);
  equal(result.report.status, 'passed', `clean fixture baseline should pass: ${JSON.stringify(result.report.blockers)}`);
  return reportPath;
}

function expectBlocked(fixture, baselinePath, name, mutateInput, expectedCode) {
  const input = designInput(fixture, baselinePath);
  mutateInput(input, fixture);
  const reportPath = path.join(fixture.evidence, name);
  const result = run(input, reportPath, 2);
  check(result.report.blockers.some(blocker => blocker.code === expectedCode), `expected blocker '${expectedCode}'`);
  return result.report;
}

function runTests() {
  const fixtures = [];
  try {
    const clean = makeFixture();
    fixtures.push(clean);
    const baseline = captureBaseline(clean);
    const designPath = path.join(clean.evidence, 'design-passed.json');
    const designResult = run(designInput(clean, baseline), designPath, 0);
    equal(designResult.report.status, 'passed', 'complete separated-contract design preflight should pass');
    check(designResult.report.target_business_contract.separate_from_owning_context_catalog, 'business and owning contracts must remain separate');
    check(designResult.report.context_path_closure.context_paths.some(item => item.path === 'result' && item.type === 'string'), 'typed expression path should close to a typed catalog field');
    check(designResult.report.context_path_closure.payload_paths.some(item => item.path === 'answer' && item.catalog_path === 'answer' && item.type === 'string'), 'payload path should be typed by the owning context catalog');
    check(!designResult.report.context_path_closure.context_paths.some(item => item.path === 'answer'), 'payload source should remain distinct from existing context inputs');
    equal(designResult.report.context_path_closure.memory_read_assets.length, 1, 'MemoryRead should resolve one real rooted file');

    const missingInputFixture = makeFixture();
    fixtures.push(missingInputFixture);
    const missingInputBaseline = captureBaseline(missingInputFixture);
    expectBlocked(missingInputFixture, missingInputBaseline, 'missing-input.json', input => {
      input.required_input_sources = input.required_input_sources.filter(entry => entry.path !== 'answer');
    }, 'required_input_namespace_unknown');

    const missingFieldFixture = makeFixture();
    fixtures.push(missingFieldFixture);
    const missingFieldBaseline = captureBaseline(missingFieldFixture);
    const missingCatalog = JSON.parse(fs.readFileSync(missingFieldFixture.catalogPath, 'utf8'));
    missingCatalog.context_fields.fields = missingCatalog.context_fields.fields.filter(field => field.path !== 'result');
    json(missingFieldFixture.catalogPath, missingCatalog);
    expectBlocked(missingFieldFixture, missingFieldBaseline, 'missing-field.json', () => {}, 'context_path_missing_from_catalog');

    const missingPayloadFieldFixture = makeFixture();
    fixtures.push(missingPayloadFieldFixture);
    const missingPayloadFieldBaseline = captureBaseline(missingPayloadFieldFixture);
    const missingPayloadCatalog = JSON.parse(fs.readFileSync(missingPayloadFieldFixture.catalogPath, 'utf8'));
    missingPayloadCatalog.context_fields.fields = missingPayloadCatalog.context_fields.fields.filter(field => field.path !== 'answer');
    json(missingPayloadFieldFixture.catalogPath, missingPayloadCatalog);
    expectBlocked(missingPayloadFieldFixture, missingPayloadFieldBaseline, 'missing-payload-field.json', () => {}, 'payload_path_missing_from_catalog');

    const ownerFixture = makeFixture();
    fixtures.push(ownerFixture);
    const ownerBaseline = captureBaseline(ownerFixture);
    const wrongOwnerCatalog = JSON.parse(fs.readFileSync(ownerFixture.catalogPath, 'utf8'));
    wrongOwnerCatalog.context_fields.fields.find(field => field.path === 'approval').owner = 'runtime';
    json(ownerFixture.catalogPath, wrongOwnerCatalog);
    expectBlocked(ownerFixture, ownerBaseline, 'owner-mismatch.json', () => {}, 'field_metadata_mismatch');

    const pathFixture = makeFixture();
    fixtures.push(pathFixture);
    const pathBaseline = captureBaseline(pathFixture);
    const unsafeWorkflow = JSON.parse(fs.readFileSync(pathFixture.workflowPath, 'utf8'));
    unsafeWorkflow.nodes['transition.memory'].command.parameters.filePaths = ['../outside.md'];
    json(pathFixture.workflowPath, unsafeWorkflow);
    expectBlocked(pathFixture, pathBaseline, 'memory-traversal.json', input => {
      input.source_files.workflow.sha256 = hash(pathFixture.workflowPath);
      input.authoritative_fields = designInput(pathFixture, pathBaseline).authoritative_fields;
    }, 'memory_read_path_unsafe');

    const extraFixture = makeFixture();
    fixtures.push(extraFixture);
    const extraFile = path.join(extraFixture.target, 'unlisted.txt');
    fs.writeFileSync(extraFile, 'not classified\n', 'utf8');
    git(extraFixture.repository, ['add', '--all']);
    git(extraFixture.repository, ['-c', 'user.name=Fixture', '-c', 'user.email=fixture@example.test', 'commit', '--quiet', '-m', 'include unlisted asset']);
    const extraBaseline = captureBaseline(extraFixture);
    expectBlocked(extraFixture, extraBaseline, 'unclassified-asset.json', () => {}, 'asset_inventory_unclassified_file');

    const staleFixture = makeFixture();
    fixtures.push(staleFixture);
    const staleBaseline = captureBaseline(staleFixture);
    fs.appendFileSync(path.join(staleFixture.target, 'SKILL.md'), 'changed after baseline\n', 'utf8');
    expectBlocked(staleFixture, staleBaseline, 'stale-baseline.json', () => {}, 'target_inventory_drift');

    const collisionFixture = makeFixture();
    fixtures.push(collisionFixture);
    const collisionBaseline = captureBaseline(collisionFixture);
    expectBlocked(collisionFixture, collisionBaseline, 'payload-context-collision.json', input => {
      input.required_input_sources.find(entry => entry.path === 'answer').namespace = 'context';
    }, 'payload_context_path_collision');

    const probeMismatchFixture = makeFixture();
    fixtures.push(probeMismatchFixture);
    const probeMismatchBaseline = captureBaseline(probeMismatchFixture);
    expectBlocked(probeMismatchFixture, probeMismatchBaseline, 'catalog-probe-mismatch.json', (input, fixture) => {
      const probe = JSON.parse(fs.readFileSync(fixture.runtimeProbePath, 'utf8'));
      probe.catalog.returned_bytes += 1;
      json(fixture.runtimeProbePath, probe);
    }, 'runtime_catalog_probe_response_mismatch');

    const packageMismatchFixture = makeFixture();
    fixtures.push(packageMismatchFixture);
    const packageMismatchBaseline = captureBaseline(packageMismatchFixture);
    expectBlocked(packageMismatchFixture, packageMismatchBaseline, 'package-validation-mismatch.json', (input, fixture) => {
      const validation = JSON.parse(fs.readFileSync(fixture.runtimePackageValidationPath, 'utf8'));
      validation.version = 'stale-fixture-runtime';
      json(fixture.runtimePackageValidationPath, validation);
    }, 'runtime_package_evidence_mismatch');

    const toolMismatchFixture = makeFixture();
    fixtures.push(toolMismatchFixture);
    const toolMismatchBaseline = captureBaseline(toolMismatchFixture);
    expectBlocked(toolMismatchFixture, toolMismatchBaseline, 'probe-tool-mismatch.json', (input, fixture) => {
      const probe = JSON.parse(fs.readFileSync(fixture.runtimeProbePath, 'utf8'));
      probe.probe_tool.sha256 = '0'.repeat(64);
      json(fixture.runtimeProbePath, probe);
    }, 'runtime_probe_tool_mismatch');

    const headFixture = makeFixture();
    fixtures.push(headFixture);
    const headReport = run(baselineInput(headFixture, { expected_head: '0'.repeat(40) }), path.join(headFixture.evidence, 'head-mismatch.json'), 2);
    check(headReport.report.blockers.some(blocker => blocker.code === 'head_mismatch'), 'unexpected repository HEAD must block');

    const reportFixture = makeFixture();
    fixtures.push(reportFixture);
    const targetReport = path.join(reportFixture.target, 'preflight-report.json');
    const targetInput = path.join(reportFixture.evidence, 'target-report.input.json');
    json(targetInput, baselineInput(reportFixture));
    const targetResult = spawnSync(process.execPath, [script, '--input-file', targetInput, '--report-file', targetReport], { encoding: 'utf8', windowsHide: true });
    equal(targetResult.status, 2, 'report destinations inside target skill root must fail');
    check(!fs.existsSync(targetReport), 'blocked target-root report must not be written');

    console.log(`preflight fixture tests passed: ${checks} checks`);
  } finally {
    for (const fixture of fixtures) fs.rmSync(fixture.root, { recursive: true, force: true });
  }
}

runTests();
