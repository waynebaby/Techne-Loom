#!/usr/bin/env node
'use strict';

const path = require('path');
const common = require('./migration-common');

const usage = `Usage: node convert-noop-to-stateupdate.js --workflow-file <path> --candidate-file <path> --manifest-file <path> [--dry-run]

Only unambiguous toolCall/noop transitions whose parameters contain exactly one literal updates object are converted.
The source workflow is never overwritten.`;

common.finish(() => {
  const args = common.parseArgs(process.argv.slice(2), ['--dry-run', '--help']);
  common.assertKnownArgs(args, ['--workflow-file', '--input-file', '--candidate-file', '--manifest-file', '--dry-run', '--help']);
  if (args['--help']) {
    process.stdout.write(`${usage}\n`);
    return 0;
  }

  const inputPath = common.resolveExistingFile(common.requireOneOf(args, ['--workflow-file', '--input-file']), 'Workflow input');
  const candidatePath = common.resolveDestination(common.requireOption(args, '--candidate-file'), 'Candidate output');
  const manifestPath = common.resolveDestination(common.requireOption(args, '--manifest-file'), 'Manifest output');
  common.assertDistinctPaths([
    ['workflow input', inputPath],
    ['candidate output', candidatePath],
    ['manifest output', manifestPath],
  ]);

  const { value: workflow } = common.readJsonFile(inputPath, 'Workflow input');
  if (!common.isRecord(workflow?.nodes)) common.fail('Workflow input must contain a nodes object.');
  const candidate = common.cloneJson(workflow);
  const manifest = common.createManifest({
    action: 'convert-noop-to-stateupdate',
    scriptPath: path.resolve(__filename),
    inputPath,
    candidatePath,
    dryRun: Boolean(args['--dry-run']),
  });

  for (const [nodeId, node] of Object.entries(candidate.nodes)) {
    if (!common.isRecord(node) || !common.getCommand(node)) continue;
    const name = common.commandName(node);
    const kind = common.normalizedStepKind(node);
    if (name !== 'noop' || kind !== 'toolcall') continue;

    manifest.targets.scanned.push(nodeId);
    const parameters = common.getParameters(node);
    if (!parameters) {
      manifest.targets.failed.push({ id: nodeId, reason: 'parameters_not_object' });
      manifest.ambiguities.push({ id: nodeId, reason: 'noop parameters must be an object.' });
      continue;
    }
    if (!common.hasOwn(parameters, 'updates')) {
      manifest.targets.unchanged.push({ id: nodeId, reason: 'noop_has_no_literal_updates' });
      continue;
    }
    if (!common.isRecord(parameters.updates) || Object.keys(parameters.updates).length === 0) {
      manifest.targets.failed.push({ id: nodeId, reason: 'updates_must_be_non_empty_object' });
      manifest.ambiguities.push({ id: nodeId, reason: 'The noop updates value is missing, empty, or not an object.' });
      continue;
    }
    if (!['tool', 'nativecode'].includes(common.commandKind(node))) {
      manifest.targets.failed.push({ id: nodeId, reason: 'unsupported_command_kind' });
      manifest.ambiguities.push({ id: nodeId, reason: 'Only tool or nativeCode noop commands have an unambiguous conversion.' });
      continue;
    }
    const extraParameters = Object.keys(parameters).filter(key => key !== 'updates');
    if (extraParameters.length > 0) {
      manifest.targets.failed.push({ id: nodeId, reason: 'extra_parameters_make_conversion_ambiguous', extra_parameters: extraParameters.sort() });
      manifest.ambiguities.push({ id: nodeId, reason: 'Noop parameters contain fields other than literal updates.', extra_parameters: extraParameters.sort() });
      continue;
    }

    const updateKeys = Object.keys(parameters.updates).sort();
    node.stepKind = 'stateUpdate';
    node.command.kind = 'nativeCode';
    node.command.name = 'state.update';
    manifest.targets.changed.push({
      id: nodeId,
      from: { stepKind: 'toolCall', command: 'noop' },
      to: { stepKind: 'stateUpdate', command: 'state.update' },
      update_keys: updateKeys,
    });
  }

  if (manifest.targets.failed.length > 0) {
    manifest.status = 'failed';
    manifest.validation.push({ check: 'candidate_write', status: 'skipped', reason: 'ambiguous_targets_present' });
    common.writeManifest(manifestPath, manifest);
    process.stdout.write(`${JSON.stringify({ status: manifest.status, manifest: manifestPath, failed: manifest.targets.failed.length })}\n`);
    return 2;
  }

  common.writeJsonNoOverwrite(candidatePath, candidate);
  common.setCandidateWritten(manifest, candidatePath);
  manifest.status = manifest.targets.changed.length > 0 ? 'changed' : 'unchanged';
  manifest.validation.push({ check: 'source_untouched', status: common.hashFile(inputPath) === manifest.source.sha256 ? 'passed' : 'failed' });
  manifest.validation.push({ check: 'candidate_json', status: 'passed' });
  common.writeManifest(manifestPath, manifest);
  process.stdout.write(`${JSON.stringify({ status: manifest.status, manifest: manifestPath, candidate: candidatePath, changed: manifest.targets.changed.length })}\n`);
  return 0;
});
