#!/usr/bin/env node
'use strict';

const path = require('path');
const common = require('./migration-common');

const usage = `Usage: node strip-result-bindings.js --workflow-file <path> --candidate-file <path> --manifest-file <path> [--dry-run]

Only $result bindings from known-null or non-producing literal writers are removed. Real tool and external-result emitters are preserved.
Unknown emitters and ambiguous binding locations fail closed. The source workflow is never overwritten.`;

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
    action: 'strip-result-bindings',
    scriptPath: path.resolve(__filename),
    inputPath,
    candidatePath,
    dryRun: Boolean(args['--dry-run']),
  });

  const graph = common.buildWorkflowGraph(candidate);
  const context = common.computeGuaranteedContextPaths(candidate, graph, true);
  for (const [nodeId, node] of common.orderedTransitions(candidate)) {
    const info = common.getOutputBindingInfo(node);
    if (info.error) {
      manifest.targets.failed.push({ id: nodeId, reason: 'duplicate_or_invalid_output_bindings', detail: info.error });
      manifest.ambiguities.push({ id: nodeId, reason: info.error });
      continue;
    }
    if (!info.bindings) continue;

    const bindings = info.bindings;
    const resultFamilies = Object.entries(bindings)
      .filter(([, value]) => value === '$result')
      .map(([family]) => family)
      .sort();
    const contextFamilies = Object.entries(bindings)
      .filter(([, value]) => typeof value === 'string' && (value.startsWith('$context:') || value.startsWith('$context.')))
      .map(([family]) => family)
      .sort();
    const allFamilies = Array.from(new Set([...resultFamilies, ...contextFamilies])).sort();
    if (allFamilies.length === 0) continue;

    manifest.targets.scanned.push(nodeId);
    const transition = Object.assign({ id: nodeId }, node);
    const emitter = common.classifyEmitter(transition);
    let changed = false;
    let failed = false;
    const removedFamilies = [];
    const preservedFamilies = [];

    for (const family of resultFamilies) {
      if (common.emitterResultIsLegitimate(transition)) {
        preservedFamilies.push({ family, reason: 'legitimate_result_emitter' });
        continue;
      }
      if (emitter !== 'known_null' && emitter !== 'literal_writer') {
        failed = true;
        manifest.ambiguities.push({ id: nodeId, reason: 'cannot_prove_result_emitter', emitter, family });
        continue;
      }
      delete bindings[family];
      removedFamilies.push(family);
      changed = true;
    }

    for (const family of contextFamilies) {
      const binding = bindings[family];
      if (typeof binding === 'string' && binding.startsWith('$context.')) {
        failed = true;
        manifest.ambiguities.push({ id: nodeId, reason: 'legacy_context_binding_syntax', family, binding });
        continue;
      }
      const concrete = common.isConcreteProducer(
        transition,
        family,
        context.initialPaths,
        graph,
        context.guaranteed,
        true);
      if (concrete) {
        preservedFamilies.push({ family, reason: 'proven_context_projection' });
      } else {
        failed = true;
        const sourcePath = common.getContextBindingPath(binding);
        manifest.ambiguities.push({
          id: nodeId,
          reason: sourcePath === family ? 'same_transition_self_binding' : 'context_binding_not_guaranteed',
          family,
          binding,
        });
      }
    }

    if (failed) {
      manifest.targets.failed.push({ id: nodeId, reason: 'ambiguous_or_unproven_binding', emitter, families: allFamilies });
      continue;
    }
    if (changed) {
      manifest.targets.changed.push({ id: nodeId, emitter, removed_families: removedFamilies, preserved_families: preservedFamilies });
    } else {
      manifest.targets.unchanged.push({ id: nodeId, reason: 'legitimate_or_proven_bindings', emitter, families: allFamilies });
    }
  }
  if (manifest.targets.failed.length > 0) {
    manifest.status = 'failed';
    manifest.validation.push({ check: 'candidate_write', status: 'skipped', reason: 'ambiguous_result_emitters_present' });
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
