'use strict';

const crypto = require('crypto');
const fs = require('fs');
const path = require('path');

class MigrationError extends Error {
  constructor(message, exitCode = 2) {
    super(message);
    this.name = 'MigrationError';
    this.exitCode = exitCode;
  }
}

function fail(message, exitCode = 2) {
  throw new MigrationError(message, exitCode);
}

function parseArgs(values, booleanKeys = []) {
  const result = {};
  const flags = new Set(booleanKeys);
  for (let index = 0; index < values.length; index += 1) {
    const value = values[index];
    if (!value.startsWith('--')) {
      fail(`Unexpected positional argument '${value}'. Inputs must be file paths passed through named options.`);
    }
    if (flags.has(value)) {
      if (Object.prototype.hasOwnProperty.call(result, value)) {
        fail(`Argument '${value}' was provided more than once.`);
      }
      result[value] = true;
      continue;
    }
    if (index + 1 >= values.length || values[index + 1].startsWith('--')) {
      fail(`Argument '${value}' requires a file path or value.`);
    }
    if (Object.prototype.hasOwnProperty.call(result, value)) {
      fail(`Argument '${value}' was provided more than once.`);
    }
    result[value] = values[++index];
  }
  return result;
}

function assertKnownArgs(args, allowed) {
  const allowedSet = new Set(allowed);
  for (const key of Object.keys(args)) {
    if (!allowedSet.has(key)) fail(`Unknown argument '${key}'.`);
  }
}

function requireOption(args, key) {
  if (typeof args[key] !== 'string' || args[key].trim().length === 0) {
    fail(`Missing required argument '${key}'.`);
  }
  return args[key];
}

function requireOneOf(args, keys) {
  const present = keys.filter(key => typeof args[key] === 'string' && args[key].trim().length > 0);
  if (present.length !== 1) {
    fail(`Provide exactly one of ${keys.map(key => `'${key}'`).join(', ')}.`);
  }
  return args[present[0]];
}

function resolveExistingFile(value, label) {
  const filePath = path.resolve(value);
  if (!fs.existsSync(filePath) || !fs.statSync(filePath).isFile()) {
    fail(`${label} '${filePath}' does not exist as a file.`);
  }
  return filePath;
}

function resolveDestination(value, label) {
  const filePath = path.resolve(value);
  if (fs.existsSync(filePath)) {
    fail(`${label} '${filePath}' already exists; choose a new candidate or evidence path.`);
  }
  return filePath;
}

function assertDistinctPaths(entries) {
  const seen = new Map();
  for (const [label, value] of entries) {
    const normalized = path.normalize(path.resolve(value)).toLowerCase();
    if (seen.has(normalized)) {
      fail(`${label} and ${seen.get(normalized)} must use different paths.`);
    }
    seen.set(normalized, label);
  }
}

function readJsonFile(filePath, label) {
  const source = fs.readFileSync(filePath, 'utf8');
  try {
    return { value: JSON.parse(source), source };
  } catch (error) {
    fail(`${label} '${filePath}' is invalid JSON: ${error.message}`);
  }
}

function writeJsonNoOverwrite(filePath, value) {
  fs.mkdirSync(path.dirname(filePath), { recursive: true });
  const text = `${JSON.stringify(value, null, 2)}\n`;
  let descriptor;
  try {
    descriptor = fs.openSync(filePath, 'wx');
    fs.writeFileSync(descriptor, text, 'utf8');
  } finally {
    if (descriptor !== undefined) fs.closeSync(descriptor);
  }
}

function hashBytes(bytes) {
  return crypto.createHash('sha256').update(bytes).digest('hex');
}

function hashFile(filePath) {
  return hashBytes(fs.readFileSync(filePath));
}

function cloneJson(value) {
  return JSON.parse(JSON.stringify(value));
}

function canonicalize(value) {
  if (Array.isArray(value)) return value.map(canonicalize);
  if (isRecord(value)) {
    return Object.keys(value).sort((left, right) => left.localeCompare(right)).reduce((result, key) => {
      result[key] = canonicalize(value[key]);
      return result;
    }, {});
  }
  return value;
}

function canonicalJson(value) {
  return JSON.stringify(canonicalize(value));
}

function hashCanonical(value) {
  return hashBytes(Buffer.from(canonicalJson(value), 'utf8'));
}

function manifestProjection(manifest) {
  const projection = cloneJson(manifest);
  delete projection.canonical_manifest_sha256;
  removePathFields(projection);
  return projection;
}

function removePathFields(value) {
  if (Array.isArray(value)) {
    value.forEach(removePathFields);
    return;
  }
  if (!isRecord(value)) return;
  for (const key of Object.keys(value)) {
    if (key === 'path' || key.endsWith('_path') || key.endsWith('_file')) {
      delete value[key];
    } else {
      removePathFields(value[key]);
    }
  }
}

function createManifest({ action, scriptPath, inputPath, candidatePath, dryRun }) {
  return {
    schema_version: 'so-migration-manifest.v1',
    action,
    mode: dryRun ? 'dry-run' : 'candidate',
    source: {
      path: inputPath,
      sha256: hashFile(inputPath),
      untouched: true,
    },
    candidate: {
      path: candidatePath,
      sha256: null,
      status: 'not_written',
    },
    script: {
      path: scriptPath,
      sha256: hashFile(scriptPath),
    },
    targets: {
      scanned: [],
      changed: [],
      unchanged: [],
      failed: [],
    },
    ambiguities: [],
    validation: [],
  };
}

function setCandidateWritten(manifest, candidatePath) {
  manifest.candidate.path = candidatePath;
  manifest.candidate.sha256 = hashFile(candidatePath);
  manifest.candidate.status = 'written';
}

function writeManifest(manifestPath, manifest) {
  manifest.canonical_manifest_sha256 = hashCanonical(manifestProjection(manifest));
  writeJsonNoOverwrite(manifestPath, manifest);
}

function isRecord(value) {
  return value !== null && typeof value === 'object' && !Array.isArray(value);
}

function hasOwn(value, key) {
  return isRecord(value) && Object.prototype.hasOwnProperty.call(value, key);
}

function getCommand(transition) {
  return isRecord(transition) && isRecord(transition.command) ? transition.command : null;
}

function getParameters(transition) {
  const command = getCommand(transition);
  if (!command || command.parameters === undefined) return {};
  return isRecord(command.parameters) ? command.parameters : null;
}

function stepKind(transition) {
  return typeof transition?.stepKind === 'string' ? transition.stepKind : '';
}

function normalizedStepKind(transition) {
  return stepKind(transition).replace(/[^A-Za-z0-9]/g, '').toLowerCase();
}

function commandName(transition) {
  const command = getCommand(transition);
  return typeof command?.name === 'string' ? command.name : '';
}

function commandKind(transition) {
  const command = getCommand(transition);
  return typeof command?.kind === 'string' ? command.kind.toLowerCase() : '';
}

function getUpdates(transition) {
  const parameters = getParameters(transition);
  return parameters && isRecord(parameters.updates) ? parameters.updates : null;
}

function getOutputBindingInfo(transition) {
  const parameters = getParameters(transition);
  const locations = [];
  if (parameters && hasOwn(parameters, 'outputBindings')) locations.push(['command.parameters.outputBindings', parameters.outputBindings]);
  if (isRecord(transition) && hasOwn(transition, 'outputBindings')) locations.push(['transition.outputBindings', transition.outputBindings]);
  if (locations.length > 1) return { error: 'outputBindings is declared in more than one location.' };
  if (locations.length === 0) return { bindings: null, location: null };
  if (!isRecord(locations[0][1])) return { error: `${locations[0][0]} must be an object.` };
  return { bindings: locations[0][1], location: locations[0][0] };
}

function getOutputBindings(transition) {
  const info = getOutputBindingInfo(transition);
  return info.bindings || {};
}

function declaredUpdateKeys(transition) {
  const updates = getUpdates(transition);
  return updates ? Object.keys(updates).filter(key => key.trim().length > 0) : [];
}

function pathCovers(parent, child) {
  if (typeof parent !== 'string' || typeof child !== 'string' || parent.length === 0 || child.length === 0) return false;
  return parent === child || child.startsWith(`${parent}.`);
}

function valueIsNonEmptyString(value) {
  return typeof value === 'string' && value.trim().length > 0;
}

function classifyEmitter(transition) {
  const kind = normalizedStepKind(transition);
  if (['modelthink', 'plan', 'mcpcall', 'subagentcall', 'askuser', 'waitresume'].includes(kind)) return 'external_result';
  if (['stateupdate', 'memorywrite', 'memoryread'].includes(kind)) return 'literal_writer';
  if (kind === 'artifactemit') return 'real_tool_result';

  const name = commandName(transition);
  if (name === 'noop') return 'known_null';
  if (!['tool', 'nativecode'].includes(commandKind(transition))) return name ? 'real_tool_result' : 'unknown';
  const parameters = getParameters(transition) || {};
  if (name === 'echo') return parameters.message !== undefined && parameters.message !== null ? 'real_tool_result' : 'known_null';
  if (name === 'ls') return 'real_tool_result';
  if (name === 'write-file' && valueIsNonEmptyString(parameters.path)) return 'real_tool_result';
  if (name === 'workflow.materializeRuntimeCopy' && valueIsNonEmptyString(parameters.sourceTemplatePath)) return 'real_tool_result';
  return 'unknown';
}

function emitterResultIsLegitimate(transition) {
  const emitter = classifyEmitter(transition);
  if (emitter === 'external_result' || emitter === 'real_tool_result') return true;
  if (emitter !== 'literal_writer') return false;
  const kind = normalizedStepKind(transition);
  if (kind === 'memoryread') return true;
  const outputPath = typeof transition.outputPath === 'string' ? transition.outputPath : '';
  return outputPath.length > 0 && declaredUpdateKeys(transition).some(key => pathCovers(key, outputPath));
}

function outputPathIsLegitimate(transition) {
  const emitter = classifyEmitter(transition);
  if (emitter === 'external_result' || emitter === 'real_tool_result') return true;
  if (emitter !== 'literal_writer') return false;
  if (normalizedStepKind(transition) === 'memoryread') return true;
  const outputPath = typeof transition.outputPath === 'string' ? transition.outputPath : '';
  return outputPath.length > 0 && declaredUpdateKeys(transition).some(key => pathCovers(key, outputPath));
}


function contextBindingPath(value) {
  if (typeof value !== 'string' || !value.startsWith('$context:')) return null;
  return value.slice('$context:'.length);
}

function isLegacyContextBinding(value) {
  return typeof value === 'string' && value.startsWith('$context.');
}

function getStringList(parameters, key) {
  if (!isRecord(parameters) || parameters[key] === undefined || parameters[key] === null) return [];
  const value = parameters[key];
  if (typeof value === 'string') return value.trim().length > 0 ? [value] : [];
  if (!Array.isArray(value)) return [];
  return value.filter(item => typeof item === 'string' && item.trim().length > 0);
}

function getPayloadPaths(transition) {
  const parameters = getParameters(transition) || {};
  return Array.from(new Set([
    ...getStringList(parameters, 'requiredInputs'),
    ...(valueIsNonEmptyString(parameters.resumeOutputKey) ? [parameters.resumeOutputKey] : []),
  ])).sort();
}

function getProducedContextPaths(transition) {
  const outputPath = typeof transition?.outputPath === 'string' ? transition.outputPath : '';
  const kind = normalizedStepKind(transition);
  const literalUpdatePaths = ['stateupdate', 'memorywrite'].includes(kind)
    ? declaredUpdateKeys(transition)
    : [];
  return Array.from(new Set([
    ...(outputPath.length > 0 ? [outputPath] : []),
    ...Object.keys(getOutputBindings(transition)),
    ...literalUpdatePaths,
  ])).sort();
}

function buildWorkflowGraph(workflow) {
  const nodes = isRecord(workflow?.nodes) ? workflow.nodes : {};
  const states = new Set(Object.entries(nodes)
    .filter(([, node]) => isRecord(node) && (node.$kind === 'state' || Array.isArray(node.groups)))
    .map(([id]) => id));
  const transitions = new Map(Object.entries(nodes)
    .filter(([, node]) => isRecord(node) && getCommand(node)));
  const reachableStates = new Set();
  const reachableTransitions = new Set();
  const sourceStates = new Map(Array.from(transitions.keys(), id => [id, []]));
  const queue = typeof workflow?.startNodeId === 'string' && workflow.startNodeId.length > 0 ? [workflow.startNodeId] : [];

  while (queue.length > 0) {
    const stateId = queue.shift();
    if (reachableStates.has(stateId) || !states.has(stateId)) continue;
    reachableStates.add(stateId);
    const state = nodes[stateId];
    const transitionIds = Array.isArray(state.groups)
      ? state.groups.flatMap(group => Array.isArray(group?.transitionIds) ? group.transitionIds : [])
      : [];
    for (const transitionId of transitionIds) {
      if (!transitions.has(transitionId)) continue;
      reachableTransitions.add(transitionId);
      const sources = sourceStates.get(transitionId);
      if (sources && !sources.includes(stateId)) sources.push(stateId);
      const target = transitions.get(transitionId)?.targetNodeId;
      if (typeof target === 'string' && states.has(target)) queue.push(target);
    }
  }

  const backEdges = new Set();
  const active = new Set();
  const completed = new Set();
  function edgeKey(source, target, transitionId) {
    return `${source}\\u0000${target}\\u0000${transitionId}`;
  }
  function visit(stateId) {
    if (!reachableStates.has(stateId) || active.has(stateId) || completed.has(stateId)) return;
    active.add(stateId);
    const state = nodes[stateId];
    const transitionIds = Array.isArray(state?.groups)
      ? state.groups.flatMap(group => Array.isArray(group?.transitionIds) ? group.transitionIds : [])
      : [];
    for (const transitionId of transitionIds) {
      if (!reachableTransitions.has(transitionId)) continue;
      const transition = transitions.get(transitionId);
      const target = transition?.targetNodeId;
      if (typeof target !== 'string' || !reachableStates.has(target)) continue;
      if (active.has(target)) backEdges.add(edgeKey(stateId, target, transitionId));
      else if (!completed.has(target)) visit(target);
    }
    active.delete(stateId);
    completed.add(stateId);
  }
  if (typeof workflow?.startNodeId === 'string') visit(workflow.startNodeId);

  const incoming = new Map(Array.from(reachableStates, id => [id, []]));
  for (const transitionId of reachableTransitions) {
    const transition = transitions.get(transitionId);
    const target = transition?.targetNodeId;
    if (typeof target !== 'string' || !incoming.has(target)) continue;
    for (const source of sourceStates.get(transitionId) || []) {
      if (!backEdges.has(edgeKey(source, target, transitionId))) {
        incoming.get(target).push({ sourceState: source, transitionId });
      }
    }
  }
  return { nodes, states, transitions, reachableStates, reachableTransitions, sourceStates, incoming, backEdges };
}

function ownOutputSubpathIsLegitimate(transition, sourcePath, family) {
  const outputPath = typeof transition?.outputPath === 'string' ? transition.outputPath : '';
  if (!outputPath || !pathCovers(outputPath, sourcePath)) return false;
  if (sourcePath === family && outputPath === family) return false;
  const emitter = classifyEmitter(transition);
  if (emitter === 'external_result' || emitter === 'real_tool_result') return true;
  if (emitter !== 'literal_writer') return false;
  if (normalizedStepKind(transition) === 'memoryread') return true;
  return declaredUpdateKeys(transition).some(key => pathCovers(key, sourcePath));
}

function isConcreteProducer(transition, family, initialPaths, graph, guaranteedPaths, governed = true) {
  const outputPath = typeof transition?.outputPath === 'string' ? transition.outputPath : '';
  if (outputPath === family) return outputPathIsLegitimate(transition);
  const kind = normalizedStepKind(transition);
  if (['stateupdate', 'memorywrite'].includes(kind)
    && declaredUpdateKeys(transition).some(key => pathCovers(key, family))) {
    return true;
  }
  const bindings = getOutputBindings(transition);
  if (!Object.prototype.hasOwnProperty.call(bindings, family)) return false;
  const binding = bindings[family];
  if (binding === '$result') return emitterResultIsLegitimate(transition);
  if (binding === null || binding === undefined) return false;
  if (typeof binding !== 'string') return false;
  if (!binding.startsWith('$context:')) return !binding.startsWith('$context.') && binding.trim().length > 0;

  const sourcePath = contextBindingPath(binding);
  if (!sourcePath) return false;
  if (initialPaths.has(sourcePath)) return true;
  if (getPayloadPaths(transition).some(path => pathCovers(path, sourcePath))) return true;
  if (ownOutputSubpathIsLegitimate(transition, sourcePath, family)) return true;
  if (!governed) return true;
  const sources = graph?.sourceStates?.get(transition.id || transition.transitionId) || [];
  return sources.length > 0 && sources.every(stateId => {
    const paths = guaranteedPaths?.get(stateId);
    return paths && Array.from(paths).some(path => pathCovers(path, sourcePath));
  });
}

function computeGuaranteedContextPaths(workflow, graph, governed = true) {
  const initialPaths = initialContextPaths(workflow);
  const reachableStates = graph.reachableStates;
  const allProducedPaths = new Set(initialPaths);
  for (const transitionId of graph.reachableTransitions) {
    for (const producedPath of getProducedContextPaths(graph.transitions.get(transitionId))) allProducedPaths.add(producedPath);
  }
  const guaranteed = new Map();
  for (const stateId of graph.states) {
    if (stateId === workflow.startNodeId) guaranteed.set(stateId, new Set(initialPaths));
    else if (reachableStates.has(stateId) && !governed) guaranteed.set(stateId, new Set(allProducedPaths));
    else guaranteed.set(stateId, new Set());
  }

  let changed = true;
  while (changed) {
    changed = false;
    for (const stateId of Array.from(reachableStates).sort()) {
      if (stateId === workflow.startNodeId) continue;
      const incoming = graph.incoming.get(stateId) || [];
      let next = null;
      for (const edge of incoming) {
        const sourcePaths = guaranteed.get(edge.sourceState) || new Set();
        const candidate = new Set(sourcePaths);
        const transition = graph.transitions.get(edge.transitionId);
        for (const producedPath of getProducedContextPaths(transition)) {
          if (isConcreteProducer(transition, producedPath, initialPaths, graph, guaranteed, governed)) candidate.add(producedPath);
        }
        if (next === null) next = candidate;
        else next = new Set(Array.from(next).filter(path => candidate.has(path)));
      }
      if (next === null) next = new Set();
      const current = guaranteed.get(stateId) || new Set();
      if (current.size !== next.size || Array.from(current).some(path => !next.has(path))) {
        guaranteed.set(stateId, next);
        changed = true;
      }
    }
  }
  return { initialPaths, guaranteed };
}

function collectObjectPaths(value, prefix = '', result = new Set()) {
  if (!isRecord(value)) return result;
  for (const [key, child] of Object.entries(value)) {
    const current = prefix ? `${prefix}.${key}` : key;
    result.add(current);
    collectObjectPaths(child, current, result);
  }
  return result;
}

function initialContextPaths(workflow) {
  return collectObjectPaths(isRecord(workflow) ? workflow.context : null);
}

function orderedTransitions(workflow) {
  const nodes = isRecord(workflow?.nodes) ? workflow.nodes : {};
  const startNodeId = typeof workflow?.startNodeId === 'string' ? workflow.startNodeId : '';
  const seenStates = new Set();
  const seenTransitions = new Set();
  const queue = startNodeId ? [startNodeId] : [];
  const result = [];
  while (queue.length > 0) {
    const stateId = queue.shift();
    if (seenStates.has(stateId)) continue;
    seenStates.add(stateId);
    const state = nodes[stateId];
    if (!isRecord(state)) continue;
    const transitionIds = Array.isArray(state.groups) ? state.groups.flatMap(group => Array.isArray(group?.transitionIds) ? group.transitionIds : []) : [];
    for (const transitionId of transitionIds) {
      if (typeof transitionId !== 'string' || seenTransitions.has(transitionId)) continue;
      const transition = nodes[transitionId];
      if (!isRecord(transition)) continue;
      seenTransitions.add(transitionId);
      result.push([transitionId, transition]);
      if (typeof transition.targetNodeId === 'string') queue.push(transition.targetNodeId);
    }
  }
  for (const [transitionId, transition] of Object.entries(nodes)) {
    if (isRecord(transition) && !seenTransitions.has(transitionId) && getCommand(transition)) result.push([transitionId, transition]);
  }
  return result;
}

function finish(main) {
  try {
    const result = main();
    if (typeof result === 'number') process.exitCode = result;
  } catch (error) {
    const exitCode = error instanceof MigrationError ? error.exitCode : 2;
    process.stderr.write(`Migration failed: ${error.message}\n`);
    process.exitCode = exitCode;
  }
}

module.exports = {
  MigrationError,
  assertDistinctPaths,
  assertKnownArgs,
  canonicalJson,
  classifyEmitter,
  cloneJson,
  commandKind,
  commandName,
  createManifest,
  declaredUpdateKeys,
  emitterResultIsLegitimate,
  fail,
  finish,
  getCommand,
  getContextBindingPath: contextBindingPath,
  hasOwn,
  getOutputBindingInfo,
  getOutputBindings,
  getPayloadPaths,
  getProducedContextPaths,
  getParameters,
  getUpdates,
  buildWorkflowGraph,
  computeGuaranteedContextPaths,
  isConcreteProducer,
  hashCanonical,
  hashFile,
  initialContextPaths,
  isRecord,
  manifestProjection,
  normalizedStepKind,
  orderedTransitions,
  outputPathIsLegitimate,
  parseArgs,
  pathCovers,
  readJsonFile,
  requireOneOf,
  requireOption,
  resolveDestination,
  resolveExistingFile,
  setCandidateWritten,
  stepKind,
  valueIsNonEmptyString,
  writeJsonNoOverwrite,
  writeManifest,
};
