'use strict';

const assert = require('assert');
const crypto = require('crypto');
const fs = require('fs');
const os = require('os');
const path = require('path');
const { spawnSync } = require('child_process');

const scriptRoot = __dirname;
const scripts = {
  convert: path.join(scriptRoot, 'convert-noop-to-stateupdate.js'),
  strip: path.join(scriptRoot, 'strip-result-bindings.js'),
  audit: path.join(scriptRoot, 'audit-output-family-producers.js'),
  idempotence: path.join(scriptRoot, 'verify-migration-idempotence.js'),
};

let checks = 0;

function check(condition, message) {
  assert.ok(condition, message);
  checks += 1;
}

function equal(actual, expected, message) {
  assert.strictEqual(actual, expected, message);
  checks += 1;
}

function writeJson(filePath, value) {
  fs.mkdirSync(path.dirname(filePath), { recursive: true });
  fs.writeFileSync(filePath, `${JSON.stringify(value, null, 2)}\n`, 'utf8');
}

function readJson(filePath) {
  return JSON.parse(fs.readFileSync(filePath, 'utf8'));
}

function hashFile(filePath) {
  return crypto.createHash('sha256').update(fs.readFileSync(filePath)).digest('hex');
}

function runScript(name, args, expectedStatus = 0) {
  const result = spawnSync(process.execPath, [scripts[name], ...args], {
    encoding: 'utf8',
    windowsHide: true,
  });
  if (result.error) throw result.error;
  const output = `${result.stdout || ''}${result.stderr || ''}`;
  equal(result.status, expectedStatus, `${name} expected exit ${expectedStatus}, got ${result.status}: ${output}`);
  return result;
}

function state(id, transitionIds) {
  return {
    $kind: 'state',
    groups: [{ transitionIds }],
  };
}

function baseWorkflow(nodes, outputFamilies = []) {
  return {
    templateKind: 'fixture',
    startNodeId: 'state.start',
    outputFamilies,
    nodes: {
      'state.start': state('state.start', Object.keys(nodes).filter(id => id.startsWith('transition.'))),
      ...nodes,
      'state.done': state('state.done', []),
    },
  };
}

function conversionWorkflow() {
  return baseWorkflow({
    'transition.write': {
      stepKind: 'toolCall',
      targetNodeId: 'state.done',
      command: {
        kind: 'tool',
        name: 'noop',
        parameters: {
          updates: {
            'migration.ready': true,
          },
        },
      },
    },
    'transition.unchanged': {
      stepKind: 'toolCall',
      targetNodeId: 'state.done',
      command: {
        kind: 'tool',
        name: 'noop',
        parameters: {},
      },
    },
  });
}

function bindingWorkflow() {
  return baseWorkflow({
    'transition.null': {
      stepKind: 'toolCall',
      targetNodeId: 'state.done',
      command: {
        kind: 'tool',
        name: 'noop',
        parameters: {
          outputBindings: {
            'null.family': '$result',
          },
        },
      },
    },
    'transition.state': {
      stepKind: 'stateUpdate',
      targetNodeId: 'state.done',
      outputPath: 'state.family',
      command: {
        kind: 'nativeCode',
        name: 'state.update',
        parameters: {
          updates: {
            'state.family': 'recorded',
          },
          outputBindings: {
            'state.family': '$result',
          },
        },
      },
    },
    'transition.echo': {
      stepKind: 'toolCall',
      targetNodeId: 'state.done',
      command: {
        kind: 'tool',
        name: 'echo',
        parameters: {
          message: 'hello-0.3.282',
          outputBindings: {
            'echo.family': '$result',
          },
        },
      },
    },
    'transition.write-file': {
      stepKind: 'toolCall',
      targetNodeId: 'state.done',
      command: {
        kind: 'tool',
        name: 'write-file',
        parameters: {
          path: 'fixture-result.txt',
          content: 'fixture',
          outputBindings: {
            'write.family': '$result',
          },
        },
      },
    },
    'transition.review': {
      stepKind: 'waitResume',
      targetNodeId: 'state.done',
      outputPath: 'review_root',
      command: {
        kind: 'nativeCode',
        name: 'workflow.requestReview',
        parameters: {
          resumeOutputKey: 'review_payload',
          requiredInputs: ['review_payload'],
          outputBindings: {
            'review_payload': '$context:review_payload',
            'review.family': '$context:review_payload.findings',
          },
        },
      },
    },
  }, ['state.family', 'echo.family', 'write.family', 'review.family']);
}

function invalidProducerWorkflow() {
  return baseWorkflow({
    'transition.unknown': {
      stepKind: 'toolCall',
      targetNodeId: 'state.done',
      command: {
        kind: 'tool',
        name: 'mystery-tool',
        parameters: {
          outputBindings: {
            'unknown.family': '$result',
          },
        },
      },
    },
    'transition.self': {
      stepKind: 'stateUpdate',
      targetNodeId: 'state.done',
      command: {
        kind: 'nativeCode',
        name: 'state.update',
        parameters: {
          updates: {
            status: 'ready',
          },
          outputBindings: {
            'self.family': '$context:self.family',
          },
        },
      },
    },
  }, ['unknown.family', 'self.family']);
}

function duplicateBindingWorkflow() {
  const workflow = bindingWorkflow();
  workflow.nodes['transition.echo'].outputBindings = {
    'echo.family': '$result',
  };
  return workflow;
}

function assertNoFile(filePath) {
  check(!fs.existsSync(filePath), `Expected no file at ${filePath}`);
}

function run() {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'loom-migration-fixtures-'));
  try {
    const conversionSource = path.join(root, 'conversion.source.json');
    const conversionHash = path.join(root, 'conversion.source.sha256');
    writeJson(conversionSource, conversionWorkflow());
    const sourceHashBefore = hashFile(conversionSource);
    fs.writeFileSync(conversionHash, sourceHashBefore, 'ascii');

    const firstCandidate = path.join(root, 'conversion.candidate.one.json');
    const secondCandidate = path.join(root, 'conversion.candidate.two.json');
    const firstManifest = path.join(root, 'conversion.manifest.one.json');
    const secondManifest = path.join(root, 'conversion.manifest.two.json');
    runScript('convert', [
      '--workflow-file', conversionSource,
      '--candidate-file', firstCandidate,
      '--manifest-file', firstManifest,
      '--dry-run',
    ]);
    runScript('convert', [
      '--workflow-file', conversionSource,
      '--candidate-file', secondCandidate,
      '--manifest-file', secondManifest,
      '--dry-run',
    ]);

    const firstCandidateJson = readJson(firstCandidate);
    const firstManifestJson = readJson(firstManifest);
    equal(firstManifestJson.mode, 'dry-run', 'conversion manifest should record dry-run mode');
    equal(firstManifestJson.candidate.status, 'written', 'conversion candidate should be written separately');
    equal(firstCandidateJson.nodes['transition.write'].stepKind, 'stateUpdate', 'noop literal update should become stateUpdate');
    equal(firstCandidateJson.nodes['transition.write'].command.name, 'state.update', 'converted command should be state.update');
    equal(firstCandidateJson.nodes['transition.unchanged'].command.name, 'noop', 'noop without updates should remain unchanged');
    equal(hashFile(conversionSource), sourceHashBefore, 'conversion must not mutate source');

    const idempotenceReport = path.join(root, 'conversion.idempotence.json');
    runScript('idempotence', [
      '--workflow-file', conversionSource,
      '--first-candidate-file', firstCandidate,
      '--second-candidate-file', secondCandidate,
      '--first-manifest-file', firstManifest,
      '--second-manifest-file', secondManifest,
      '--report-file', idempotenceReport,
      '--dry-run',
    ]);
    const idempotenceJson = readJson(idempotenceReport);
    equal(idempotenceJson.status, 'passed', 'independent conversion runs should be idempotent');
    check(idempotenceJson.checks.every(item => item.status === 'passed'), 'all idempotence checks should pass');

    const overwriteManifest = path.join(root, 'conversion.overwrite.manifest.json');
    runScript('convert', [
      '--workflow-file', conversionSource,
      '--candidate-file', firstCandidate,
      '--manifest-file', overwriteManifest,
    ], 2);
    equal(hashFile(conversionSource), sourceHashBefore, 'no-overwrite failure must preserve source');

    const bindingSource = path.join(root, 'bindings.source.json');
    const bindingCandidate = path.join(root, 'bindings.candidate.json');
    const bindingManifest = path.join(root, 'bindings.manifest.json');
    writeJson(bindingSource, bindingWorkflow());
    const bindingSourceHashBefore = hashFile(bindingSource);
    runScript('strip', [
      '--workflow-file', bindingSource,
      '--candidate-file', bindingCandidate,
      '--manifest-file', bindingManifest,
      '--dry-run',
    ]);
    const bindingCandidateJson = readJson(bindingCandidate);
    const bindingManifestJson = readJson(bindingManifest);
    equal(bindingManifestJson.status, 'changed', 'known-null binding cleanup should change the candidate');
    check(Object.keys(bindingCandidateJson.nodes['transition.null'].command.parameters.outputBindings).length === 0, 'known-null $result should be removed');
    equal(bindingCandidateJson.nodes['transition.state'].command.parameters.outputBindings['state.family'], '$result', 'covered stateUpdate result should remain');
    equal(bindingCandidateJson.nodes['transition.echo'].command.parameters.outputBindings['echo.family'], '$result', 'echo result should remain');
    equal(bindingCandidateJson.nodes['transition.write-file'].command.parameters.outputBindings['write.family'], '$result', 'write-file result should remain');
    equal(bindingCandidateJson.nodes['transition.review'].command.parameters.outputBindings['review.family'], '$context:review_payload.findings', 'external projection should remain');
    equal(bindingCandidateJson.nodes['transition.review'].command.parameters.outputBindings['review_payload'], '$context:review_payload', 'same-name external payload projection should remain');
    equal(hashFile(bindingSource), bindingSourceHashBefore, 'strip must not mutate source');

    const auditSource = path.join(root, 'audit.source.json');
    const auditReport = path.join(root, 'audit.report.json');
    const auditWorkflow = bindingWorkflow();
    delete auditWorkflow.nodes['transition.null'];
    auditWorkflow.nodes['state.start'].groups[0].transitionIds = auditWorkflow.nodes['state.start'].groups[0].transitionIds.filter(id => id !== 'transition.null');
    writeJson(auditSource, auditWorkflow);
    runScript('audit', [
      '--workflow-file', auditSource,
      '--report-file', auditReport,
      '--dry-run',
    ]);
    const auditJson = readJson(auditReport);
    equal(auditJson.status, 'passed', 'valid producer audit should pass');
    equal(auditJson.families.length, 6, 'valid producer audit should inspect six families including resume projections');
    check(auditJson.families.every(family => family.status === 'concrete'), 'every valid family should have one concrete producer');
    const payloadTransition = auditJson.transitions.find(item => item.id === 'transition.review');
    check(payloadTransition.findings.some(item => item.family === 'review_payload' && item.result.kind === 'concrete'), 'same-name external payload projection should be concrete');

    const invalidSource = path.join(root, 'invalid.source.json');
    const invalidCandidate = path.join(root, 'invalid.candidate.json');
    const invalidManifest = path.join(root, 'invalid.manifest.json');
    writeJson(invalidSource, invalidProducerWorkflow());
    runScript('strip', [
      '--workflow-file', invalidSource,
      '--candidate-file', invalidCandidate,
      '--manifest-file', invalidManifest,
      '--dry-run',
    ], 2);
    const invalidManifestJson = readJson(invalidManifest);
    equal(invalidManifestJson.status, 'failed', 'unknown and self-bound emitters should fail closed');
    check(invalidManifestJson.targets.failed.length >= 2, 'both invalid producer targets should be reported');
    assertNoFile(invalidCandidate);

    const invalidAuditReport = path.join(root, 'invalid.audit.report.json');
    runScript('audit', [
      '--workflow-file', invalidSource,
      '--report-file', invalidAuditReport,
      '--dry-run',
    ], 2);
    const invalidAuditJson = readJson(invalidAuditReport);
    equal(invalidAuditJson.status, 'failed', 'invalid producer audit should fail');
    check(invalidAuditJson.ambiguities.some(item => item.reason === 'unknown_emitter'), 'unknown emitter should be explicit');
    check(invalidAuditJson.families.some(item => item.family === 'self.family' && item.status === 'missing_or_ambiguous'), 'self-binding family should be unresolved');

    const duplicateSource = path.join(root, 'duplicate.source.json');
    const duplicateCandidate = path.join(root, 'duplicate.candidate.json');
    const duplicateManifest = path.join(root, 'duplicate.manifest.json');
    writeJson(duplicateSource, duplicateBindingWorkflow());
    runScript('strip', [
      '--workflow-file', duplicateSource,
      '--candidate-file', duplicateCandidate,
      '--manifest-file', duplicateManifest,
      '--dry-run',
    ], 2);
    assertNoFile(duplicateCandidate);
    const duplicateManifestJson = readJson(duplicateManifest);
    equal(duplicateManifestJson.status, 'failed', 'duplicate output binding locations should fail closed');

    const duplicateDestination = path.join(root, 'duplicate.destination.json');
    runScript('convert', [
      '--workflow-file', conversionSource,
      '--candidate-file', duplicateDestination,
      '--manifest-file', duplicateDestination,
      '--dry-run',
    ], 2);
    assertNoFile(duplicateDestination);

    const malformedSource = path.join(root, 'malformed.source.json');
    const malformedReport = path.join(root, 'malformed.report.json');
    fs.writeFileSync(malformedSource, '{\n', 'utf8');
    runScript('audit', [
      '--workflow-file', malformedSource,
      '--report-file', malformedReport,
      '--dry-run',
    ], 2);
    assertNoFile(malformedReport);

    console.log(`migration fixture tests passed: ${checks} checks`);
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
}

run();
