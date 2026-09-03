#!/usr/bin/env node
'use strict';

const path = require('path');
const common = require('./migration-common');

const usage = `Usage: node audit-output-family-producers.js --workflow-file <path> --report-file <path> [--dry-run]

Audits declared output families and reachable transition producers without changing the workflow.
Declarations, output paths, and same-transition context bindings are never treated as proof by themselves.`;

function addValues(families, values) {
  for (const value of values) {
    if (Array.isArray(value)) {
      value.filter(item => typeof item === 'string' && item.trim().length > 0).forEach(item => families.add(item));
    } else if (common.isRecord(value)) {
      Object.keys(value).filter(item => item.trim().length > 0).forEach(item => families.add(item));
    }
  }
}

function collectOutputFamilies(workflow, transitions) {
  const families = new Set();
  addValues(families, [workflow.requiredOutputFamilies, workflow.requiredOutputs, workflow.outputFamilies]);
  const gates = common.isRecord(workflow.validation?.gates) ? Object.values(workflow.validation.gates) : [];
  for (const gate of gates) {
    if (!common.isRecord(gate)) continue;
    addValues(families, [gate.requiredOutputFamilies, gate.requiredMachineReadableOutputFamilies, gate.requiredHumanReviewableOutputFamilies]);
  }
  for (const [, node] of transitions) {
    addValues(families, [node.publishesOutputFamilies, node.publishesBlockedOutputFamilies, node.outputFamilies]);
    for (const family of Object.keys(common.getOutputBindings(node))) families.add(family);
    if (typeof node.outputPath === 'string' && node.outputPath.trim().length > 0) families.add(node.outputPath);
  }
  return Array.from(families).sort();
}

function transitionProduces(id, node, family, analysis) {
  const transition = Object.assign({ id }, node);
  const emitter = common.classifyEmitter(transition);
  const outputPath = typeof transition.outputPath === 'string' ? transition.outputPath : '';
  const bindingInfo = common.getOutputBindingInfo(transition);
  if (bindingInfo.error) return { kind: 'ambiguous', reason: 'invalid_output_bindings', detail: bindingInfo.error };
  const bindings = bindingInfo.bindings || {};
  const hasBinding = Object.prototype.hasOwnProperty.call(bindings, family);

  if (outputPath === family) {
    if (common.outputPathIsLegitimate(transition)) return { kind: 'concrete', reason: 'legitimate_output_path' };
    return emitter === 'unknown'
      ? { kind: 'ambiguous', reason: 'unknown_output_path_emitter' }
      : { kind: 'pseudo', reason: 'output_path_without_legitimate_emitter' };
  }
  if (!hasBinding) {
    if (common.declaredUpdateKeys(transition).some(key => common.pathCovers(key, family))) {
      return { kind: 'pseudo', reason: 'declared_update_without_output_projection' };
    }
    return null;
  }

  const boundValue = bindings[family];
  if (boundValue === '$result') {
    if (common.emitterResultIsLegitimate(transition)) return { kind: 'concrete', reason: 'legitimate_result_emitter' };
    return emitter === 'unknown'
      ? { kind: 'ambiguous', reason: 'unknown_result_emitter' }
      : { kind: 'pseudo', reason: 'result_binding_without_legitimate_emitter' };
  }

  const sourcePath = common.getContextBindingPath(boundValue);
  if (sourcePath !== null) {
    return common.isConcreteProducer(
      transition,
      family,
      analysis.initialPaths,
      analysis.graph,
      analysis.guaranteed,
      true)
      ? { kind: 'concrete', reason: analysis.initialPaths.has(sourcePath) ? 'initial_context_value' : 'prior_context_or_payload_value' }
      : { kind: 'pseudo', reason: 'context_value_not_guaranteed_before_transition' };
  }
  if (typeof boundValue === 'string' && boundValue.startsWith('$context.')) {
    return { kind: 'ambiguous', reason: 'legacy_context_binding_syntax' };
  }
  if (typeof boundValue === 'string' && boundValue.trim().length > 0) {
    return { kind: 'concrete', reason: 'non_empty_literal_binding' };
  }
  return { kind: 'pseudo', reason: 'empty_output_binding' };
}

common.finish(() => {
  const args = common.parseArgs(process.argv.slice(2), ['--dry-run', '--help']);
  common.assertKnownArgs(args, ['--workflow-file', '--input-file', '--report-file', '--dry-run', '--help']);
  if (args['--help']) {
    process.stdout.write(`${usage}\n`);
    return 0;
  }

  const inputPath = common.resolveExistingFile(common.requireOneOf(args, ['--workflow-file', '--input-file']), 'Workflow input');
  const reportPath = common.resolveDestination(common.requireOption(args, '--report-file'), 'Audit report');
  common.assertDistinctPaths([['workflow input', inputPath], ['audit report', reportPath]]);
  const { value: workflow } = common.readJsonFile(inputPath, 'Workflow input');
  if (!common.isRecord(workflow?.nodes)) common.fail('Workflow input must contain a nodes object.');

  const graph = common.buildWorkflowGraph(workflow);
  const transitions = common.orderedTransitions(workflow).filter(([id]) => graph.reachableTransitions.has(id));
  const families = collectOutputFamilies(workflow, transitions);
  const analysis = common.computeGuaranteedContextPaths(workflow, graph, true);
  analysis.graph = graph;
  const report = {
    schema_version: 'so-output-family-producer-audit.v1',
    mode: args['--dry-run'] ? 'dry-run' : 'audit',
    source: { path: inputPath, sha256: common.hashFile(inputPath), untouched: true },
    script: { path: path.resolve(__filename), sha256: common.hashFile(__filename) },
    ordering: {
      reachable_states: graph.reachableStates.size,
      reachable_transitions: graph.reachableTransitions.size,
      back_edges_ignored_on_first_arrival: graph.backEdges.size,
    },
    transitions: [],
    families: [],
    ambiguities: [],
    status: 'passed',
  };

  for (const [id, node] of transitions) {
    const transition = Object.assign({ id }, node);
    const emitter = common.classifyEmitter(transition);
    const info = common.getOutputBindingInfo(transition);
    if (info.error) {
      report.ambiguities.push({ id, reason: 'invalid_output_bindings', detail: info.error });
      report.transitions.push({ id, emitter, status: 'ambiguous', detail: info.error });
      continue;
    }
    if (emitter === 'unknown') {
      report.ambiguities.push({ id, reason: 'unknown_emitter' });
      report.transitions.push({ id, emitter, status: 'ambiguous' });
      continue;
    }
    const findings = families
      .map(family => ({ family, result: transitionProduces(id, node, family, analysis) }))
      .filter(item => item.result);
    report.transitions.push({ id, emitter, findings });
  }

  for (const family of families) {
    const concrete = [];
    const pseudo = [];
    const ambiguous = [];
    for (const [id, node] of transitions) {
      const result = transitionProduces(id, node, family, analysis);
      if (!result) continue;
      if (result.kind === 'concrete') concrete.push({ id, reason: result.reason });
      if (result.kind === 'pseudo') pseudo.push({ id, reason: result.reason });
      if (result.kind === 'ambiguous') ambiguous.push({ id, reason: result.reason });
    }
    const status = concrete.length === 0
      ? (pseudo.length > 0 || ambiguous.length > 0 ? 'missing_or_ambiguous' : 'missing')
      : concrete.length > 1 ? 'multiple' : 'concrete';
    if (status !== 'concrete') report.status = 'failed';
    report.families.push({
      family,
      status,
      concrete_producers: concrete,
      pseudo_producers: pseudo,
      ambiguous,
    });
  }
  if (report.ambiguities.length > 0) report.status = 'failed';
  report.canonical_report_sha256 = common.hashCanonical(report);
  common.writeJsonNoOverwrite(reportPath, report);
  process.stdout.write(`${JSON.stringify({ status: report.status, report: reportPath, families: report.families.length, ambiguities: report.ambiguities.length })}\n`);
  return report.status === 'passed' ? 0 : 2;
});
