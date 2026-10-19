#!/usr/bin/env node
'use strict';

const crypto = require('crypto');
const fs = require('fs');
const path = require('path');
const { spawnSync } = require('child_process');
const common = require('./migration-common');

const inputSchema = 'loom-skill-enhancement-preflight.v1';
const reportSchema = 'loom-skill-enhancement-preflight-report.v1';
const fieldMetadataKeys = [
  'path',
  'type',
  'nullable',
  'owner',
  'description',
  'itemType',
  'valueType',
  'minimum',
  'maximum',
  'allowed_values',
  'suggested_values',
  'suggestion_reason',
];
const supportedTypes = new Set(['string', 'boolean', 'integer', 'number', 'object', 'array']);
const supportedOwners = new Set(['caller', 'user', 'agent', 'runtime']);
const assetClassifications = new Set(['required', 'generated', 'not_applicable', 'damaged_conflicting']);
const derivationSources = new Set([
  'business_contract',
  'selected_route',
  'workflow',
  'node_to_file_map',
  'document_copy_manifest',
]);
const familyPropertyNames = new Set([
  'requiredOutputFamilies',
  'requiredMachineReadableOutputFamilies',
  'requiredHumanReviewableOutputFamilies',
  'requiredOutputs',
  'publishesOutputFamilies',
  'publishesBlockedOutputFamilies',
  'outputFamilies',
]);
const sourceFileNames = new Set([
  'business_contract',
  'workflow',
  'node_to_file_map',
  'document_copy_manifest',
  'owning_context_catalog',
  'context_file',
  'runtime_guide',
  'runtime_package_validation',
  'runtime_probe',
  'runtime_probe_tool',
]);

const usage = `Usage: node preflight-target-skill.js --input-file <preflight-input.json> --report-file <preflight-report.json>

Captures a repository/target baseline or verifies a route-specific target inventory and workflow context catalog.
The command reads target files and writes only a new external report. Input files are path-only.`;

function fail(message) {
  const error = new Error(message);
  error.name = 'PreflightError';
  throw error;
}

function addBlocker(blockers, code, message, details = {}) {
  blockers.push({ code, message, ...details });
}

function isSha256(value) {
  return typeof value === 'string' && /^[a-f0-9]{64}$/i.test(value);
}

function toPosix(value) {
  return value.replace(/\\/g, '/');
}

function normalizedAbsolute(value) {
  return toPosix(path.resolve(value));
}

function samePath(left, right) {
  const first = path.resolve(left);
  const second = path.resolve(right);
  return process.platform === 'win32'
    ? first.toLowerCase() === second.toLowerCase()
    : first === second;
}

function isWithin(root, candidate) {
  const relative = path.relative(root, candidate);
  return relative === ''
    || (relative !== '..' && !relative.startsWith(`..${path.sep}`) && !path.isAbsolute(relative));
}

function resolveExistingDirectory(value, label) {
  if (typeof value !== 'string' || !path.isAbsolute(value)) {
    fail(`${label} must be an absolute directory path.`);
  }
  const resolved = path.resolve(value);
  if (!fs.existsSync(resolved) || !fs.statSync(resolved).isDirectory()) {
    fail(`${label} '${resolved}' does not exist as a directory.`);
  }
  return fs.realpathSync(resolved);
}

function safeRelativePath(value, label) {
  if (typeof value !== 'string' || value.length === 0 || value !== value.trim()) {
    fail(`${label} must be a non-empty relative path.`);
  }
  const normalizedInput = value.replace(/\\/g, '/');
  if (normalizedInput.startsWith('/') || /^[a-zA-Z]:/.test(normalizedInput)) {
    fail(`${label} '${value}' must be relative to its declared root.`);
  }
  const segments = normalizedInput.split('/');
  if (segments.some(segment => segment.length === 0 || segment === '.' || segment === '..')) {
    fail(`${label} '${value}' contains an empty, current-directory, or traversal segment.`);
  }
  return segments.join('/');
}

function resolveSourceFile(spec, roots, label) {
  if (!common.isRecord(spec) || typeof spec.base !== 'string' || typeof spec.path !== 'string') {
    fail(`${label} must declare an explicit base and path.`);
  }
  let candidate;
  let containmentRoot = null;
  let relativePath = null;
  if (spec.base === 'target_skill' || spec.base === 'repository') {
    containmentRoot = spec.base === 'target_skill' ? roots.targetRoot : roots.repositoryRoot;
    relativePath = safeRelativePath(spec.path, `${label} path`);
    candidate = path.resolve(containmentRoot, ...relativePath.split('/'));
  } else if (spec.base === 'external') {
    if (!path.isAbsolute(spec.path)) fail(`${label} external path must be absolute.`);
    candidate = path.resolve(spec.path);
  } else {
    fail(`${label} base must be 'target_skill', 'repository', or 'external'.`);
  }
  if (!fs.existsSync(candidate) || !fs.statSync(candidate).isFile()) {
    fail(`${label} '${normalizedAbsolute(candidate)}' does not exist as a file.`);
  }
  const actualPath = fs.realpathSync(candidate);
  if (containmentRoot && !isWithin(containmentRoot, actualPath)) {
    fail(`${label} resolves outside its declared ${spec.base} root.`);
  }
  return {
    base: spec.base,
    path: normalizedAbsolute(actualPath),
    relative_path: relativePath,
    sha256: common.hashFile(actualPath),
    size_bytes: fs.statSync(actualPath).size,
  };
}

function readSourceJson(source, label) {
  const { value } = common.readJsonFile(source.path, label);
  return value;
}

function runGit(repositoryRoot, args, allowFailure = false) {
  const result = spawnSync('git', ['-C', repositoryRoot, ...args], {
    encoding: 'utf8',
    windowsHide: true,
  });
  if (result.error) fail(`Unable to run git ${args[0]}: ${result.error.message}`);
  if (!allowFailure && result.status !== 0) {
    fail(`git ${args.join(' ')} failed: ${(result.stderr || result.stdout || '').trim()}`);
  }
  return { status: result.status, stdout: result.stdout || '', stderr: result.stderr || '' };
}

function listTargetFiles(targetRoot, blockers) {
  const files = [];
  function visit(directory) {
    let entries;
    try {
      entries = fs.readdirSync(directory, { withFileTypes: true })
        .sort((left, right) => left.name.localeCompare(right.name));
    } catch (error) {
      addBlocker(blockers, 'target_inventory_unreadable', `Cannot enumerate '${normalizedAbsolute(directory)}': ${error.message}`);
      return;
    }
    for (const entry of entries) {
      const fullPath = path.join(directory, entry.name);
      const relativePath = toPosix(path.relative(targetRoot, fullPath));
      if (entry.isSymbolicLink()) {
        addBlocker(blockers, 'target_inventory_symlink', `Target inventory contains a symbolic link at '${relativePath}'.`, { path: relativePath });
        continue;
      }
      if (entry.isDirectory()) {
        visit(fullPath);
        continue;
      }
      if (!entry.isFile()) {
        addBlocker(blockers, 'target_inventory_unsupported_entry', `Target inventory contains an unsupported filesystem entry at '${relativePath}'.`, { path: relativePath });
        continue;
      }
      try {
        const bytes = fs.readFileSync(fullPath);
        files.push({ path: relativePath, size_bytes: bytes.length, sha256: crypto.createHash('sha256').update(bytes).digest('hex') });
      } catch (error) {
        addBlocker(blockers, 'target_inventory_unreadable', `Cannot read target asset '${relativePath}': ${error.message}`, { path: relativePath });
      }
    }
  }
  visit(targetRoot);
  return files.sort((left, right) => left.path.localeCompare(right.path));
}

function sortedStatusLines(value, label) {
  if (!Array.isArray(value) || value.some(item => typeof item !== 'string')) {
    fail(`${label} must be an array of git porcelain status lines.`);
  }
  return value.slice().sort((left, right) => left.localeCompare(right));
}

function captureBaseline(input, blockers) {
  const repositoryRoot = resolveExistingDirectory(input.repository_root, 'Repository root');
  const targetRoot = resolveExistingDirectory(input.target_skill_root, 'Target skill root');
  const gitRootOutput = runGit(repositoryRoot, ['rev-parse', '--show-toplevel']).stdout.trim();
  const gitRoot = fs.realpathSync(gitRootOutput);
  if (!samePath(repositoryRoot, gitRoot)) {
    fail(`Declared repository root '${normalizedAbsolute(repositoryRoot)}' differs from the actual worktree root '${normalizedAbsolute(gitRoot)}'.`);
  }
  if (!isWithin(repositoryRoot, targetRoot) || samePath(repositoryRoot, targetRoot)) {
    fail(`Target skill root '${normalizedAbsolute(targetRoot)}' must be a child of the declared repository root.`);
  }

  const head = runGit(repositoryRoot, ['rev-parse', 'HEAD']).stdout.trim().toLowerCase();
  const branch = runGit(repositoryRoot, ['branch', '--show-current']).stdout.trim();
  const targetRelative = toPosix(path.relative(repositoryRoot, targetRoot)) || '.';
  const statusOutput = runGit(repositoryRoot, ['status', '--porcelain=v1', '--untracked-files=all', '--', targetRelative]).stdout;
  const targetStatus = statusOutput.split(/\r?\n/).filter(line => line.length > 0).sort((left, right) => left.localeCompare(right));
  const inventoryBlockers = [];
  const targetFiles = listTargetFiles(targetRoot, inventoryBlockers);
  blockers.push(...inventoryBlockers);
  const inventorySha256 = common.hashCanonical(targetFiles);

  const requestedRef = input.requested_ref === undefined || input.requested_ref === null
    ? null
    : input.requested_ref;
  let requestedRefHead = null;
  if (requestedRef !== null) {
    if (typeof requestedRef !== 'string' || requestedRef.trim().length === 0 || requestedRef.startsWith('-')) {
      fail('requested_ref must be a non-empty Git ref name or null.');
    }
    const resolved = runGit(repositoryRoot, ['rev-parse', '--verify', `${requestedRef}^{commit}`], true);
    if (resolved.status !== 0) {
      addBlocker(blockers, 'requested_ref_unresolved', `Requested Git ref '${requestedRef}' could not be resolved.`, { requested_ref: requestedRef });
    } else {
      requestedRefHead = resolved.stdout.trim().toLowerCase();
      if (requestedRefHead !== head) {
        addBlocker(blockers, 'requested_ref_mismatch', `Requested Git ref '${requestedRef}' does not identify the checked-out HEAD.`, {
          requested_ref: requestedRef,
          requested_ref_head: requestedRefHead,
          head,
        });
      }
    }
  }

  if (input.expected_head !== undefined && input.expected_head !== null) {
    if (!isSha256(input.expected_head) && !/^[a-f0-9]{40}$/i.test(input.expected_head)) {
      fail('expected_head must be a full Git object id or null.');
    }
    if (input.expected_head.toLowerCase() !== head) {
      addBlocker(blockers, 'head_mismatch', 'Checked-out HEAD differs from the declared baseline.', {
        expected_head: input.expected_head.toLowerCase(),
        actual_head: head,
      });
    }
  }

  if (Object.prototype.hasOwnProperty.call(input, 'expected_target_status')) {
    const expectedStatus = sortedStatusLines(input.expected_target_status, 'expected_target_status');
    if (common.canonicalJson(expectedStatus) !== common.canonicalJson(targetStatus)) {
      addBlocker(blockers, 'target_status_mismatch', 'Target Git status differs from the declared baseline.', {
        expected_target_status: expectedStatus,
        actual_target_status: targetStatus,
      });
    }
  } else if (targetStatus.length > 0) {
    addBlocker(blockers, 'unexpected_dirty_target', 'Target skill files are dirty but no expected target status was declared.', {
      actual_target_status: targetStatus,
    });
  }

  if (input.expected_inventory_sha256 !== undefined && input.expected_inventory_sha256 !== null) {
    if (!isSha256(input.expected_inventory_sha256)) fail('expected_inventory_sha256 must be a SHA-256 value or null.');
    if (input.expected_inventory_sha256.toLowerCase() !== inventorySha256) {
      addBlocker(blockers, 'target_inventory_mismatch', 'Target asset inventory differs from the declared baseline.', {
        expected_inventory_sha256: input.expected_inventory_sha256.toLowerCase(),
        actual_inventory_sha256: inventorySha256,
      });
    }
  }

  return {
    repository_root: normalizedAbsolute(repositoryRoot),
    target_skill_root: normalizedAbsolute(targetRoot),
    head,
    branch: branch || null,
    requested_ref: requestedRef,
    requested_ref_head: requestedRefHead,
    target_relative_path: targetRelative,
    target_git_status: targetStatus,
    target_inventory: targetFiles,
    target_inventory_sha256: inventorySha256,
  };
}

function validateBaselineReport(descriptor, currentSnapshot, blockers) {
  if (!common.isRecord(descriptor) || typeof descriptor.path !== 'string' || !isSha256(descriptor.sha256)) {
    fail('design phase requires baseline_report with an absolute path and SHA-256.');
  }
  if (!path.isAbsolute(descriptor.path)) fail('baseline_report.path must be absolute.');
  const reportPath = path.resolve(descriptor.path);
  if (!fs.existsSync(reportPath) || !fs.statSync(reportPath).isFile()) fail(`Baseline report '${reportPath}' does not exist as a file.`);
  const actualHash = common.hashFile(reportPath);
  if (actualHash.toLowerCase() !== descriptor.sha256.toLowerCase()) {
    addBlocker(blockers, 'baseline_report_hash_mismatch', 'Baseline report hash differs from the declared evidence.', {
      path: normalizedAbsolute(reportPath),
      expected_sha256: descriptor.sha256.toLowerCase(),
      actual_sha256: actualHash,
    });
  }
  const baseline = common.readJsonFile(reportPath, 'Baseline report').value;
  if (baseline.schema_version !== reportSchema || baseline.phase !== 'baseline' || baseline.status !== 'passed') {
    addBlocker(blockers, 'baseline_report_invalid', 'Baseline report must be a passed report from the baseline phase.', {
      path: normalizedAbsolute(reportPath),
    });
  }
  const baselineProjection = common.cloneJson(baseline);
  delete baselineProjection.canonical_report_sha256;
  if (!isSha256(baseline.canonical_report_sha256)
    || common.hashCanonical(baselineProjection) !== baseline.canonical_report_sha256) {
    addBlocker(blockers, 'baseline_report_integrity_failed', 'Baseline report canonical hash is missing or invalid.', {
      path: normalizedAbsolute(reportPath),
    });
  }
  if (!isSha256(baseline.baseline_snapshot_sha256)
    || common.hashCanonical(baseline.baseline_snapshot) !== baseline.baseline_snapshot_sha256) {
    addBlocker(blockers, 'baseline_snapshot_integrity_failed', 'Baseline snapshot hash is missing or invalid.', {
      path: normalizedAbsolute(reportPath),
    });
  }
  if (!common.isRecord(baseline.baseline_snapshot)) {
    addBlocker(blockers, 'baseline_snapshot_missing', 'Baseline report does not contain a baseline snapshot.', {
      path: normalizedAbsolute(reportPath),
    });
    return { path: normalizedAbsolute(reportPath), sha256: actualHash, snapshot: null };
  }

  const previous = baseline.baseline_snapshot;
  const identity = ['repository_root', 'target_skill_root', 'head', 'branch', 'requested_ref', 'requested_ref_head', 'target_relative_path'];
  const mismatches = identity.filter(key => common.canonicalJson(previous[key]) !== common.canonicalJson(currentSnapshot[key]));
  if (mismatches.length > 0) {
    addBlocker(blockers, 'repository_baseline_drift', 'Repository, worktree, target root, or requested ref changed after baseline capture.', {
      fields: mismatches,
    });
  }
  if (common.canonicalJson(previous.target_git_status) !== common.canonicalJson(currentSnapshot.target_git_status)) {
    addBlocker(blockers, 'target_status_drift', 'Target Git status changed after baseline capture.', {
      previous: previous.target_git_status,
      current: currentSnapshot.target_git_status,
    });
  }
  if (previous.target_inventory_sha256 !== currentSnapshot.target_inventory_sha256) {
    addBlocker(blockers, 'target_inventory_drift', 'Target asset bytes changed after baseline capture.', {
      previous_sha256: previous.target_inventory_sha256,
      current_sha256: currentSnapshot.target_inventory_sha256,
    });
  }
  return { path: normalizedAbsolute(reportPath), sha256: actualHash, snapshot: previous };
}

function resolveJsonPointer(root, pointer) {
  if (pointer === '') return root;
  if (typeof pointer !== 'string' || !pointer.startsWith('/')) return undefined;
  const tokens = pointer.slice(1).split('/').map(token => token.replace(/~1/g, '/').replace(/~0/g, '~'));
  let current = root;
  for (const token of tokens) {
    if (Array.isArray(current)) {
      if (!/^(0|[1-9][0-9]*)$/.test(token)) return undefined;
      current = current[Number(token)];
    } else if (common.isRecord(current)) {
      current = current[token];
    } else {
      return undefined;
    }
  }
  return current;
}

function validateBusinessContract(contract, blockers) {
  if (!common.isRecord(contract)) {
    addBlocker(blockers, 'business_contract_invalid', 'Target business contract must be a JSON object.');
    return null;
  }
  if (typeof contract.name !== 'string' || contract.name.trim().length === 0) {
    addBlocker(blockers, 'business_contract_name_missing', 'Target business contract must have a non-empty name.');
  }
  for (const key of ['inputs', 'outputs', 'default_assumptions']) {
    if (!common.isRecord(contract[key])) {
      addBlocker(blockers, 'business_contract_surface_missing', `Target business contract must contain an object-valued '${key}' surface.`, { path: key });
    }
  }
  return { name: typeof contract.name === 'string' ? contract.name : null };
}

function validateAssetInventory(declarations, snapshot, sourceFiles, selectedRoute, blockers) {
  if (!Array.isArray(declarations)) fail('asset_inventory must be an array.');
  const actualByPath = new Map(snapshot.target_inventory.map(item => [item.path.toLowerCase(), item]));
  const declarationByPath = new Map();
  const entries = [];
  const declaredDerivations = new Set();

  for (const declaration of declarations) {
    if (!common.isRecord(declaration)) {
      addBlocker(blockers, 'asset_inventory_entry_invalid', 'Every asset inventory entry must be an object.');
      continue;
    }
    let assetPath;
    try {
      assetPath = safeRelativePath(declaration.path, 'asset_inventory.path');
    } catch (error) {
      addBlocker(blockers, 'asset_inventory_path_invalid', error.message, { path: declaration.path ?? null });
      continue;
    }
    const key = process.platform === 'win32' ? assetPath.toLowerCase() : assetPath;
    if (declarationByPath.has(key)) {
      addBlocker(blockers, 'asset_inventory_duplicate_path', `Asset '${assetPath}' is classified more than once.`, { path: assetPath });
      continue;
    }
    if (!assetClassifications.has(declaration.classification)) {
      addBlocker(blockers, 'asset_inventory_classification_invalid', `Asset '${assetPath}' has unsupported classification '${declaration.classification}'.`, { path: assetPath });
      continue;
    }
    if (typeof declaration.reason !== 'string' || declaration.reason.trim().length === 0) {
      addBlocker(blockers, 'asset_inventory_reason_missing', `Asset '${assetPath}' requires a classification reason.`, { path: assetPath });
    }
    if (!Array.isArray(declaration.derived_from) || declaration.derived_from.length === 0
      || declaration.derived_from.some(source => !derivationSources.has(source))) {
      addBlocker(blockers, 'asset_inventory_derivation_invalid', `Asset '${assetPath}' must cite recognized derivation sources.`, { path: assetPath });
    } else {
      declaration.derived_from.forEach(source => declaredDerivations.add(source));
    }
    if (declaration.expected_sha256 !== undefined && declaration.expected_sha256 !== null && !isSha256(declaration.expected_sha256)) {
      addBlocker(blockers, 'asset_inventory_hash_invalid', `Asset '${assetPath}' has an invalid expected SHA-256.`, { path: assetPath });
    }
    declarationByPath.set(key, declaration);
    const actual = actualByPath.get(key) || null;
    let observedStatus = declaration.classification;
    if (declaration.classification === 'required' && actual === null) {
      observedStatus = 'missing_required';
      addBlocker(blockers, 'required_asset_missing', `Required target asset '${assetPath}' is missing.`, { path: assetPath });
    } else if (declaration.classification === 'required' && actual !== null) {
      if (!isSha256(declaration.expected_sha256)) {
        observedStatus = 'missing_expected_hash';
        addBlocker(blockers, 'required_asset_hash_missing', `Required target asset '${assetPath}' must carry its baseline SHA-256.`, { path: assetPath });
      } else if (declaration.expected_sha256.toLowerCase() !== actual.sha256) {
        observedStatus = 'damaged_conflicting';
        addBlocker(blockers, 'required_asset_hash_mismatch', `Required target asset '${assetPath}' changed from its declared hash.`, {
          path: assetPath,
          expected_sha256: declaration.expected_sha256.toLowerCase(),
          actual_sha256: actual.sha256,
        });
      } else {
        observedStatus = 'required_present';
      }
    } else if (declaration.classification === 'generated') {
      if (actual === null) {
        observedStatus = 'generated_missing';
      } else if (!isSha256(declaration.expected_sha256)) {
        observedStatus = 'generated_existing_unhashed';
        addBlocker(blockers, 'generated_asset_hash_missing', `Existing generated asset '${assetPath}' must be hashed before it can be treated as safe input.`, { path: assetPath });
      } else if (declaration.expected_sha256.toLowerCase() !== actual.sha256) {
        observedStatus = 'damaged_conflicting';
        addBlocker(blockers, 'generated_asset_hash_mismatch', `Existing generated asset '${assetPath}' differs from its declared hash.`, {
          path: assetPath,
          expected_sha256: declaration.expected_sha256.toLowerCase(),
          actual_sha256: actual.sha256,
        });
      } else {
        observedStatus = 'generated_existing';
      }
    } else if (declaration.classification === 'damaged_conflicting') {
      observedStatus = 'damaged_conflicting';
      addBlocker(blockers, 'asset_declared_damaged_conflicting', `Asset '${assetPath}' is explicitly marked damaged or conflicting.`, { path: assetPath });
    } else {
      observedStatus = actual === null ? 'not_applicable_missing' : 'not_applicable_present';
    }
    entries.push({
      path: assetPath,
      classification: declaration.classification,
      observed_status: observedStatus,
      reason: typeof declaration.reason === 'string' ? declaration.reason : '',
      derived_from: Array.isArray(declaration.derived_from) ? declaration.derived_from.slice().sort() : [],
      expected_sha256: declaration.expected_sha256 ?? null,
      actual_sha256: actual?.sha256 ?? null,
      actual_size_bytes: actual?.size_bytes ?? null,
    });
  }

  for (const actual of snapshot.target_inventory) {
    const key = process.platform === 'win32' ? actual.path.toLowerCase() : actual.path;
    if (!declarationByPath.has(key)) {
      addBlocker(blockers, 'asset_inventory_unclassified_file', `Existing target asset '${actual.path}' has no route-specific classification.`, { path: actual.path, actual_sha256: actual.sha256 });
    }
  }

  for (const requiredSource of ['business_contract', 'selected_route', 'workflow', 'node_to_file_map', 'document_copy_manifest']) {
    const source = requiredSource === 'selected_route' ? selectedRoute : sourceFiles[requiredSource];
    if (source && !declaredDerivations.has(requiredSource)) {
      addBlocker(blockers, 'asset_inventory_source_not_covered', `Route-specific inventory does not cite '${requiredSource}' as a derivation source.`, { source: requiredSource });
    }
  }

  for (const key of ['business_contract', 'workflow', 'node_to_file_map', 'document_copy_manifest']) {
    const source = sourceFiles[key];
    if (!source || source.base !== 'target_skill') continue;
    const declaration = declarationByPath.get(process.platform === 'win32' ? source.relative_path.toLowerCase() : source.relative_path);
    if (!declaration || declaration.classification !== 'required') {
      addBlocker(blockers, 'source_asset_not_required', `Current source '${key}' must be listed as a required target asset.`, {
        source: key,
        path: source.relative_path,
      });
    }
  }

  return {
    status: blockers.length === 0 ? 'passed' : 'blocked',
    inventory_sha256: snapshot.target_inventory_sha256,
    actual_file_count: snapshot.target_inventory.length,
    classified_entry_count: entries.length,
    entries: entries.sort((left, right) => left.path.localeCompare(right.path)),
  };
}

function validContextPath(value) {
  return typeof value === 'string'
    && value.length > 0
    && value === value.trim()
    && !value.includes('/')
    && !value.includes('\\')
    && value.split('.').every(segment => segment.length > 0 && segment === segment.trim());
}

function addPath(pathMap, value, source, blockers, namespace = 'context') {
  if (!validContextPath(value)) {
    addBlocker(blockers, 'context_path_invalid', `Invalid ${namespace} path '${value}'.`, { path: value ?? null, source });
    return;
  }
  if (!pathMap.has(value)) pathMap.set(value, new Set());
  pathMap.get(value).add(source);
}

function collectObjectContextPaths(value, pathPrefix, source, contextPaths, blockers) {
  if (!common.isRecord(value)) return;
  for (const [key, child] of Object.entries(value)) {
    const current = pathPrefix ? `${pathPrefix}.${key}` : key;
    addPath(contextPaths, current, source, blockers);
    if (common.isRecord(child)) collectObjectContextPaths(child, current, source, contextPaths, blockers);
  }
}

function collectFamilyValues(value, location, contextPaths, blockers) {
  const values = Array.isArray(value)
    ? value
    : (typeof value === 'string' ? [value] : (common.isRecord(value) ? Object.keys(value) : []));
  for (const item of values) addPath(contextPaths, item, location, blockers);
}

function collectExpressions(value, location, results, inExpression = false) {
  if (typeof value === 'string') {
    if (inExpression) results.push({ source: value, location });
    return;
  }
  if (Array.isArray(value)) {
    value.forEach((item, index) => collectExpressions(item, `${location}/${index}`, results, inExpression));
    return;
  }
  if (!common.isRecord(value)) return;
  for (const [key, child] of Object.entries(value)) {
    const expressionValue = inExpression || /expression/i.test(key);
    collectExpressions(child, `${location}/${key}`, results, expressionValue);
  }
}

function mapClrType(typeName) {
  const type = typeName.replace(/^global::/, '').replace(/\?/g, '').replace(/\s/g, '');
  const simple = type.replace(/^System\./, '');
  const scalarTypes = new Map([
    ['string', { type: 'string' }],
    ['String', { type: 'string' }],
    ['bool', { type: 'boolean' }],
    ['Boolean', { type: 'boolean' }],
    ['byte', { type: 'integer' }],
    ['sbyte', { type: 'integer' }],
    ['short', { type: 'integer' }],
    ['Int16', { type: 'integer' }],
    ['ushort', { type: 'integer' }],
    ['UInt16', { type: 'integer' }],
    ['int', { type: 'integer' }],
    ['Int32', { type: 'integer' }],
    ['uint', { type: 'integer' }],
    ['UInt32', { type: 'integer' }],
    ['long', { type: 'integer' }],
    ['Int64', { type: 'integer' }],
    ['ulong', { type: 'integer' }],
    ['UInt64', { type: 'integer' }],
    ['float', { type: 'number' }],
    ['Single', { type: 'number' }],
    ['double', { type: 'number' }],
    ['Double', { type: 'number' }],
    ['decimal', { type: 'number' }],
    ['Decimal', { type: 'number' }],
  ]);
  if (scalarTypes.has(simple)) return scalarTypes.get(simple);
  if (simple.endsWith('[]')) {
    const item = mapClrType(simple.slice(0, -2));
    return item ? { type: 'array', itemType: item.type } : null;
  }
  const listMatch = simple.match(/^(?:List|IList|IReadOnlyList|IEnumerable|ICollection|IReadOnlyCollection)<(.+)>$/);
  if (listMatch) {
    const item = mapClrType(listMatch[1]);
    return item ? { type: 'array', itemType: item.type } : null;
  }
  const mapMatch = simple.match(/^(?:Dictionary|IDictionary|IReadOnlyDictionary)<string,(.+)>$/);
  if (mapMatch) {
    const value = mapClrType(mapMatch[1]);
    return value ? { type: 'object', valueType: value.type } : null;
  }
  return null;
}

function parseExpressionReads(expressions, contextPaths, typedReads, blockers) {
  for (const expression of expressions) {
    const source = expression.source;
    let getMatches = 0;
    let hasMatches = 0;
    const getPattern = /\bcontext\s*\.\s*Get\s*<([^>]+)>\s*\(\s*"([^"]+)"\s*\)/g;
    let match;
    while ((match = getPattern.exec(source)) !== null) {
      getMatches += 1;
      const [, typeName, contextPath] = match;
      addPath(contextPaths, contextPath, expression.location, blockers);
      const expected = mapClrType(typeName.trim());
      if (!expected) {
        addBlocker(blockers, 'expression_type_unresolved', `Cannot map C# context read type '${typeName.trim()}' to an exact JSON field type.`, {
          path: contextPath,
          location: expression.location,
        });
      }
      typedReads.push({ path: contextPath, csharp_type: typeName.trim(), expected, location: expression.location });
    }
    const hasPattern = /\bcontext\s*\.\s*Has\s*\(\s*"([^"]+)"\s*\)/g;
    while ((match = hasPattern.exec(source)) !== null) {
      hasMatches += 1;
      addPath(contextPaths, match[1], expression.location, blockers);
    }
    const indexPattern = /\bcontext\s*\[([^\]]*)\]/g;
    while ((match = indexPattern.exec(source)) !== null) {
      const literal = match[1].match(/^\s*"([^"]+)"\s*$/);
      if (literal) addPath(contextPaths, literal[1], expression.location, blockers);
      else addBlocker(blockers, 'expression_path_dynamic', 'Context indexer path is not a literal string and cannot be closed statically.', { location: expression.location });
    }
    const getCalls = (source.match(/\bcontext\s*\.\s*Get\s*<[^>]+>\s*\(/g) || []).length;
    const hasCalls = (source.match(/\bcontext\s*\.\s*Has\s*\(/g) || []).length;
    if (getCalls !== getMatches || hasCalls !== hasMatches) {
      addBlocker(blockers, 'expression_path_dynamic', 'A context.Get or context.Has call does not use a supported literal path.', {
        location: expression.location,
        get_calls: getCalls,
        resolved_get_calls: getMatches,
        has_calls: hasCalls,
        resolved_has_calls: hasMatches,
      });
    }
  }
}

function strictStringList(value, label, blockers) {
  if (value === undefined || value === null) return [];
  const values = typeof value === 'string' ? [value] : value;
  if (!Array.isArray(values) || values.some(item => typeof item !== 'string' || item.trim().length === 0)) {
    addBlocker(blockers, 'workflow_string_list_invalid', `${label} must be a string or an array of non-empty strings.`);
    return [];
  }
  return values;
}

function collectDeclaredPaths(value, location, contextPaths, blockers) {
  const values = Array.isArray(value) ? value : [];
  for (const entry of values) {
    const pathValue = typeof entry === 'string' ? entry : (common.isRecord(entry) ? entry.path : null);
    addPath(contextPaths, pathValue, location, blockers);
  }
}

function validateRequiredInputSources(workflow, inputMappings, transitions, contextPaths, payloadPaths, blockers) {
  if (!Array.isArray(inputMappings)) fail('required_input_sources must be an array.');
  const mappings = new Map();
  for (const mapping of inputMappings) {
    if (!common.isRecord(mapping)
      || typeof mapping.transition_id !== 'string'
      || !validContextPath(mapping.path)
      || !['context', 'payload'].includes(mapping.namespace)
      || typeof mapping.reason !== 'string'
      || mapping.reason.trim().length === 0) {
      addBlocker(blockers, 'required_input_source_invalid', 'Every required_input_sources entry must identify a transition, path namespace, and reason.');
      continue;
    }
    const key = JSON.stringify([mapping.transition_id, mapping.path]);
    if (mappings.has(key)) {
      addBlocker(blockers, 'required_input_source_duplicate', `Required input '${mapping.path}' on '${mapping.transition_id}' has multiple namespace declarations.`, {
        transition_id: mapping.transition_id,
        path: mapping.path,
      });
      continue;
    }
    mappings.set(key, mapping);
  }

  const used = new Set();
  for (const [transitionId, transition] of transitions) {
    const parameters = common.getParameters(transition);
    if (parameters === null) {
      addBlocker(blockers, 'transition_parameters_invalid', `Transition '${transitionId}' command parameters must be an object.`, { transition_id: transitionId });
      continue;
    }
    for (const requiredPath of strictStringList(parameters.requiredInputs, `${transitionId}.requiredInputs`, blockers)) {
      if (!validContextPath(requiredPath)) {
        addBlocker(blockers, 'required_input_path_invalid', `Transition '${transitionId}' has invalid required input path '${requiredPath}'.`, {
          transition_id: transitionId,
          path: requiredPath,
        });
        continue;
      }
      const key = JSON.stringify([transitionId, requiredPath]);
      const mapping = mappings.get(key);
      if (!mapping) {
        addBlocker(blockers, 'required_input_namespace_unknown', `Required input '${requiredPath}' on '${transitionId}' is not classified as payload-relative or workflow context.`, {
          transition_id: transitionId,
          path: requiredPath,
        });
        continue;
      }
      used.add(key);
      if (mapping.namespace === 'context') addPath(contextPaths, requiredPath, `requiredInputs:${transitionId}`, blockers);
      else addPath(payloadPaths, requiredPath, `requiredInputs:${transitionId}`, blockers, 'payload');
    }
  }
  for (const [key, mapping] of mappings) {
    if (!used.has(key) || !transitions.has(mapping.transition_id)) {
      addBlocker(blockers, 'required_input_source_unused', `Namespace declaration for '${mapping.path}' does not match a required input on '${mapping.transition_id}'.`, {
        transition_id: mapping.transition_id,
        path: mapping.path,
      });
    }
  }
  return mappings;
}

function validateMemoryReadInputs(workflow, inputEntries, transitions, targetRoot, assetInventory, blockers) {
  if (!Array.isArray(inputEntries)) fail('memory_read_inputs must be an array.');
  const memoryTransitions = new Map(Array.from(transitions).filter(([, node]) => common.normalizedStepKind(node) === 'memoryread'));
  const byTransition = new Map();
  const results = [];
  for (const entry of inputEntries) {
    if (!common.isRecord(entry)
      || typeof entry.transition_id !== 'string'
      || typeof entry.parameter_pointer !== 'string'
      || typeof entry.reason !== 'string'
      || entry.reason.trim().length === 0) {
      addBlocker(blockers, 'memory_read_input_invalid', 'Every memory_read_inputs entry must identify a transition, parameter pointer, and reason.');
      continue;
    }
    const transition = memoryTransitions.get(entry.transition_id);
    if (!transition) {
      addBlocker(blockers, 'memory_read_transition_unknown', `MemoryRead source '${entry.transition_id}' is not a MemoryRead transition.`, { transition_id: entry.transition_id });
      continue;
    }
    if (!entry.parameter_pointer.startsWith('/')) {
      addBlocker(blockers, 'memory_read_pointer_invalid', `MemoryRead parameter pointer '${entry.parameter_pointer}' must be a JSON Pointer below command.parameters.`, { transition_id: entry.transition_id });
      continue;
    }
    const parameters = common.getParameters(transition);
    const value = resolveJsonPointer(parameters, entry.parameter_pointer);
    const values = typeof value === 'string' ? [value] : value;
    if (!Array.isArray(values) || values.some(item => typeof item !== 'string' || item.trim().length === 0)) {
      addBlocker(blockers, 'memory_read_paths_unresolved', `MemoryRead pointer '${entry.parameter_pointer}' must identify a path or an array of paths.`, {
        transition_id: entry.transition_id,
        parameter_pointer: entry.parameter_pointer,
      });
      continue;
    }
    if (!byTransition.has(entry.transition_id)) byTransition.set(entry.transition_id, new Set());
    byTransition.get(entry.transition_id).add(entry.parameter_pointer);
    for (const item of values) {
      let relativePath;
      try {
        relativePath = safeRelativePath(item, `MemoryRead path on ${entry.transition_id}`);
      } catch (error) {
        addBlocker(blockers, 'memory_read_path_unsafe', error.message, { transition_id: entry.transition_id, path: item });
        continue;
      }
      const absolutePath = path.resolve(targetRoot, ...relativePath.split('/'));
      if (!isWithin(targetRoot, absolutePath) || !fs.existsSync(absolutePath) || !fs.statSync(absolutePath).isFile()) {
        addBlocker(blockers, 'memory_read_asset_missing', `MemoryRead path '${relativePath}' does not resolve to a file under the target skill root.`, {
          transition_id: entry.transition_id,
          path: relativePath,
        });
        continue;
      }
      const actualPath = fs.realpathSync(absolutePath);
      if (!isWithin(targetRoot, actualPath)) {
        addBlocker(blockers, 'memory_read_asset_escape', `MemoryRead path '${relativePath}' resolves outside the target skill root.`, {
          transition_id: entry.transition_id,
          path: relativePath,
        });
        continue;
      }
      const inventoryEntry = assetInventory.entries.find(asset => asset.path === relativePath);
      if (!inventoryEntry || inventoryEntry.classification !== 'required' || inventoryEntry.observed_status !== 'required_present') {
        addBlocker(blockers, 'memory_read_asset_not_required', `MemoryRead asset '${relativePath}' is not classified as a present required target asset.`, {
          transition_id: entry.transition_id,
          path: relativePath,
        });
      }
      results.push({
        transition_id: entry.transition_id,
        parameter_pointer: entry.parameter_pointer,
        path: relativePath,
        sha256: common.hashFile(actualPath),
        reason: entry.reason,
      });
    }
  }
  for (const [transitionId] of memoryTransitions) {
    if (!byTransition.has(transitionId)) {
      addBlocker(blockers, 'memory_read_inputs_missing', `MemoryRead transition '${transitionId}' has no explicit target-root path mapping.`, { transition_id: transitionId });
    }
  }
  return results.sort((left, right) => `${left.transition_id}:${left.parameter_pointer}:${left.path}`.localeCompare(`${right.transition_id}:${right.parameter_pointer}:${right.path}`));
}

function collectWorkflowClosure(workflow, contextFile, input, targetRoot, assetInventory, blockers) {
  const contextPaths = new Map();
  const payloadPaths = new Map();
  const typedReads = [];
  const expressions = [];
  collectExpressions(workflow, 'workflow', expressions);
  parseExpressionReads(expressions, contextPaths, typedReads, blockers);
  collectObjectContextPaths(workflow.context, 'workflow.context', 'workflow.context', contextPaths, blockers);
  collectObjectContextPaths(contextFile, '', 'context_file', contextPaths, blockers);

  const transitions = Object.entries(workflow.nodes)
    .filter(([, node]) => common.isRecord(node) && common.getCommand(node));
  const transitionMap = new Map(transitions);
  const inputMappings = validateRequiredInputSources(workflow, input.required_input_sources, transitionMap, contextPaths, payloadPaths, blockers);
  const memoryReadAssets = validateMemoryReadInputs(workflow, input.memory_read_inputs, transitionMap, targetRoot, assetInventory, blockers);

  const addFamilyFields = value => {
    if (Array.isArray(value)) value.forEach(item => addPath(contextPaths, item, 'output_family', blockers));
    else if (typeof value === 'string') addPath(contextPaths, value, 'output_family', blockers);
    else if (common.isRecord(value)) Object.keys(value).forEach(item => addPath(contextPaths, item, 'output_family', blockers));
  };
  function scanFamilyDeclarations(value, location) {
    if (Array.isArray(value)) {
      value.forEach((item, index) => scanFamilyDeclarations(item, `${location}/${index}`));
      return;
    }
    if (!common.isRecord(value)) return;
    for (const [key, child] of Object.entries(value)) {
      if (familyPropertyNames.has(key)) addFamilyFields(child);
      else if (key === 'valueSemantics' && common.isRecord(child)) Object.keys(child).forEach(item => addPath(contextPaths, item, `${location}/valueSemantics`, blockers));
      else scanFamilyDeclarations(child, `${location}/${key}`);
    }
  }
  scanFamilyDeclarations(workflow, 'workflow');
  collectDeclaredPaths(workflow.validation?.declaredUserOwnedFields, 'declaredUserOwnedFields', contextPaths, blockers);
  collectDeclaredPaths(workflow.validation?.reservedRuntimeOwnedFields, 'reservedRuntimeOwnedFields', contextPaths, blockers);

  for (const [transitionId, transition] of transitions) {
    if (typeof transition.outputPath === 'string' && transition.outputPath.trim().length > 0) {
      addPath(contextPaths, transition.outputPath, `outputPath:${transitionId}`, blockers);
    }
    const updates = common.getUpdates(transition);
    if (updates) {
      for (const updatePath of Object.keys(updates)) addPath(contextPaths, updatePath, `updates:${transitionId}`, blockers);
    }
    const parameters = common.getParameters(transition) || {};
    if (typeof parameters.resumeOutputKey === 'string' && parameters.resumeOutputKey.trim().length > 0) {
      addPath(payloadPaths, parameters.resumeOutputKey, `resumeOutputKey:${transitionId}`, blockers, 'payload');
    }
    const bindingInfo = common.getOutputBindingInfo(transition);
    if (bindingInfo.error) {
      addBlocker(blockers, 'output_binding_invalid', bindingInfo.error, { transition_id: transitionId });
      continue;
    }
    for (const [destination, binding] of Object.entries(bindingInfo.bindings || {})) {
      addPath(contextPaths, destination, `outputBindingDestination:${transitionId}`, blockers);
      if (typeof binding !== 'string') continue;
      if (binding.startsWith('$context:')) {
        addPath(contextPaths, binding.slice('$context:'.length), `outputBindingSource:${transitionId}`, blockers);
      } else if (binding.startsWith('$payload:')) {
        const sourcePath = binding.slice('$payload:'.length);
        addPath(payloadPaths, sourcePath, `outputBindingSource:${transitionId}`, blockers, 'payload');
        const mapping = inputMappings.get(JSON.stringify([transitionId, sourcePath]));
        if (!mapping || mapping.namespace !== 'payload') {
          addBlocker(blockers, 'payload_projection_not_required', `Payload projection '${sourcePath}' on '${transitionId}' lacks a payload-relative requiredInputs declaration.`, {
            transition_id: transitionId,
            path: sourcePath,
          });
        }
      } else if (binding.startsWith('$context.')) {
        addBlocker(blockers, 'legacy_context_binding', `Legacy context binding '${binding}' is not a typed source path.`, {
          transition_id: transitionId,
          binding,
        });
      } else if (binding.startsWith('$') && binding !== '$result') {
        addBlocker(blockers, 'output_binding_source_unknown', `Output binding '${binding}' has no supported source namespace.`, {
          transition_id: transitionId,
          binding,
        });
      }
    }
  }

  for (const payloadPath of payloadPaths.keys()) {
    if (contextPaths.has(payloadPath)) {
      addBlocker(blockers, 'payload_context_path_collision', `Path '${payloadPath}' is declared in both the payload and workflow-context namespaces.`, { path: payloadPath });
    }
  }

  return {
    contextPaths,
    payloadPaths,
    typedReads,
    memoryReadAssets,
  };
}

function validateFieldMetadata(metadata, location, blockers) {
  if (!common.isRecord(metadata)) {
    addBlocker(blockers, 'field_metadata_invalid', `${location} metadata must be an object.`);
    return;
  }
  for (const key of ['path', 'type', 'nullable', 'owner', 'description']) {
    if (!Object.prototype.hasOwnProperty.call(metadata, key)) {
      addBlocker(blockers, 'field_metadata_required', `${location} is missing required metadata '${key}'.`, { path: metadata.path ?? null, key });
    }
  }
  if (typeof metadata.path !== 'string' || !validContextPath(metadata.path)) {
    addBlocker(blockers, 'field_path_invalid', `${location} has an invalid context path.`, { path: metadata.path ?? null });
  }
  if (!supportedTypes.has(metadata.type)) {
    addBlocker(blockers, 'field_type_invalid', `${location} has unsupported JSON type '${metadata.type}'.`, { path: metadata.path ?? null, type: metadata.type ?? null });
  }
  if (typeof metadata.nullable !== 'boolean') {
    addBlocker(blockers, 'field_nullable_invalid', `${location} must declare boolean nullable.`, { path: metadata.path ?? null });
  }
  if (!supportedOwners.has(metadata.owner)) {
    addBlocker(blockers, 'field_owner_invalid', `${location} has unsupported owner '${metadata.owner}'.`, { path: metadata.path ?? null, owner: metadata.owner ?? null });
  }
  if (typeof metadata.description !== 'string' || metadata.description.trim().length === 0) {
    addBlocker(blockers, 'field_description_missing', `${location} must declare a non-empty description.`, { path: metadata.path ?? null });
  }
  if (metadata.type === 'array' && !supportedTypes.has(metadata.itemType)) {
    addBlocker(blockers, 'field_item_type_invalid', `${location} array itemType must be a supported JSON type.`, { path: metadata.path ?? null });
  }
  if (metadata.type !== 'array' && Object.prototype.hasOwnProperty.call(metadata, 'itemType')) {
    addBlocker(blockers, 'field_item_type_unexpected', `${location} itemType is valid only for array fields.`, { path: metadata.path ?? null });
  }
  if (metadata.type === 'object' && metadata.valueType !== undefined && !supportedTypes.has(metadata.valueType)) {
    addBlocker(blockers, 'field_value_type_invalid', `${location} object valueType must be a supported JSON type.`, { path: metadata.path ?? null });
  }
  if (metadata.type !== 'object' && Object.prototype.hasOwnProperty.call(metadata, 'valueType')) {
    addBlocker(blockers, 'field_value_type_unexpected', `${location} valueType is valid only for object fields.`, { path: metadata.path ?? null });
  }
  if (Object.prototype.hasOwnProperty.call(metadata, 'minimum') && !Number.isFinite(metadata.minimum)) {
    addBlocker(blockers, 'field_minimum_invalid', `${location} minimum must be a finite number.`, { path: metadata.path ?? null });
  }
  if (Object.prototype.hasOwnProperty.call(metadata, 'maximum') && !Number.isFinite(metadata.maximum)) {
    addBlocker(blockers, 'field_maximum_invalid', `${location} maximum must be a finite number.`, { path: metadata.path ?? null });
  }
  if (metadata.minimum !== undefined && metadata.maximum !== undefined && metadata.minimum > metadata.maximum) {
    addBlocker(blockers, 'field_numeric_bounds_invalid', `${location} minimum exceeds maximum.`, { path: metadata.path ?? null });
  }
  if (Object.prototype.hasOwnProperty.call(metadata, 'allowed_values') && !Array.isArray(metadata.allowed_values)) {
    addBlocker(blockers, 'field_allowed_values_invalid', `${location} allowed_values must be an array.`, { path: metadata.path ?? null });
  }
  if (Object.prototype.hasOwnProperty.call(metadata, 'suggested_values') && !Array.isArray(metadata.suggested_values)) {
    addBlocker(blockers, 'field_suggested_values_invalid', `${location} suggested_values must be an array.`, { path: metadata.path ?? null });
  }
  if (Object.prototype.hasOwnProperty.call(metadata, 'suggestion_reason') && typeof metadata.suggestion_reason !== 'string') {
    addBlocker(blockers, 'field_suggestion_reason_invalid', `${location} suggestion_reason must be a string.`, { path: metadata.path ?? null });
  }
  const unknownKeys = Object.keys(metadata).filter(key => !fieldMetadataKeys.includes(key));
  if (unknownKeys.length > 0) {
    addBlocker(blockers, 'field_metadata_unknown_keys', `${location} contains unsupported field metadata keys.`, {
      path: metadata.path ?? null,
      keys: unknownKeys.sort(),
    });
  }
}

function metadataProjection(metadata) {
  const projection = {};
  for (const key of fieldMetadataKeys) {
    if (Object.prototype.hasOwnProperty.call(metadata, key)) projection[key] = metadata[key];
  }
  return projection;
}

function validateFieldAuthority(catalogFields, authoritativeFields, sourceFiles, blockers) {
  const catalogByPath = new Map();
  for (const field of catalogFields) {
    if (!common.isRecord(field)) {
      addBlocker(blockers, 'catalog_field_invalid', 'Every owning catalog field must be an object.');
      continue;
    }
    validateFieldMetadata(field, `catalog field '${field.path ?? '<missing>'}'`, blockers);
    if (typeof field.path !== 'string') continue;
    if (catalogByPath.has(field.path)) {
      addBlocker(blockers, 'catalog_field_duplicate', `Owning catalog contains duplicate path '${field.path}'.`, { path: field.path });
      continue;
    }
    catalogByPath.set(field.path, field);
  }

  if (!Array.isArray(authoritativeFields)) {
    addBlocker(blockers, 'authoritative_fields_missing', 'authoritative_fields must be an array of sourced metadata declarations.');
    return { catalogByPath, mismatches: [] };
  }
  const authoritativeByPath = new Map();
  const mismatches = [];
  for (const entry of authoritativeFields) {
    if (!common.isRecord(entry) || !common.isRecord(entry.metadata)) {
      addBlocker(blockers, 'authoritative_field_invalid', 'Every authoritative_fields entry must contain metadata and evidence.');
      continue;
    }
    const metadata = entry.metadata;
    validateFieldMetadata(metadata, `authoritative field '${metadata.path ?? '<missing>'}'`, blockers);
    if (typeof metadata.path !== 'string') continue;
    if (authoritativeByPath.has(metadata.path)) {
      addBlocker(blockers, 'authoritative_field_duplicate', `Authoritative declarations duplicate path '${metadata.path}'.`, { path: metadata.path });
      continue;
    }
    authoritativeByPath.set(metadata.path, entry);
    if (!Array.isArray(entry.evidence) || entry.evidence.length === 0) {
      addBlocker(blockers, 'field_evidence_missing', `Authoritative field '${metadata.path}' has no source evidence.`, { path: metadata.path });
    } else {
      for (const evidence of entry.evidence) {
        if (!common.isRecord(evidence)
          || typeof evidence.source_file !== 'string'
          || !isSha256(evidence.sha256)
          || typeof evidence.locator !== 'string'
          || evidence.locator.trim().length === 0) {
          addBlocker(blockers, 'field_evidence_invalid', `Authoritative field '${metadata.path}' has incomplete source evidence.`, { path: metadata.path });
          continue;
        }
        const source = sourceFiles[evidence.source_file];
        if (!source || evidence.source_file === 'owning_context_catalog') {
          addBlocker(blockers, 'field_evidence_source_invalid', `Field '${metadata.path}' cites an unavailable or self-authorizing source '${evidence.source_file}'.`, {
            path: metadata.path,
            source_file: evidence.source_file,
          });
        } else if (source.sha256 !== evidence.sha256.toLowerCase()) {
          addBlocker(blockers, 'field_evidence_hash_mismatch', `Field '${metadata.path}' source hash does not match current disk content.`, {
            path: metadata.path,
            source_file: evidence.source_file,
            expected_sha256: evidence.sha256.toLowerCase(),
            actual_sha256: source.sha256,
          });
        }
      }
    }
    const catalogField = catalogByPath.get(metadata.path);
    if (!catalogField) {
      mismatches.push({ path: metadata.path, issue: 'authoritative_field_missing_from_catalog' });
      continue;
    }
    const expected = common.canonicalJson(metadataProjection(metadata));
    const actual = common.canonicalJson(metadataProjection(catalogField));
    if (expected !== actual) mismatches.push({ path: metadata.path, issue: 'metadata_mismatch' });
  }

  for (const pathValue of catalogByPath.keys()) {
    if (!authoritativeByPath.has(pathValue)) mismatches.push({ path: pathValue, issue: 'catalog_field_without_authority' });
  }
  for (const mismatch of mismatches) {
    addBlocker(blockers, 'field_metadata_mismatch', `Owning catalog metadata for '${mismatch.path}' does not exactly match its sourced declaration.`, mismatch);
  }
  return { catalogByPath, mismatches };
}

function resolveCatalogField(pathValue, catalogByPath) {
  if (catalogByPath.has(pathValue)) return { metadata: catalogByPath.get(pathValue), declared_at: pathValue };
  const separator = pathValue.lastIndexOf('.');
  if (separator <= 0) return null;
  const parentPath = pathValue.slice(0, separator);
  const property = pathValue.slice(separator + 1);
  const parent = catalogByPath.get(parentPath);
  if (!parent || parent.type !== 'object' || typeof parent.valueType !== 'string' || property.includes('.')) return null;
  return {
    declared_at: parentPath,
    metadata: {
      path: pathValue,
      type: parent.valueType,
      nullable: false,
      owner: parent.owner,
      description: parent.description,
      ...(parent.minimum !== undefined ? { minimum: parent.minimum } : {}),
      ...(parent.maximum !== undefined ? { maximum: parent.maximum } : {}),
    },
  };
}

function validateCatalogClosure(closure, catalogByPath, blockers) {
  const contextPaths = [];
  for (const [pathValue, sources] of closure.contextPaths) {
    const resolved = resolveCatalogField(pathValue, catalogByPath);
    if (!resolved) {
      addBlocker(blockers, 'context_path_missing_from_catalog', `Workflow context path '${pathValue}' has no exact owning catalog declaration.`, {
        path: pathValue,
        sources: Array.from(sources).sort(),
      });
    }
    contextPaths.push({
      path: pathValue,
      sources: Array.from(sources).sort(),
      catalog_path: resolved?.declared_at ?? null,
      type: resolved?.metadata.type ?? null,
      itemType: resolved?.metadata.itemType ?? null,
      valueType: resolved?.metadata.valueType ?? null,
      nullable: resolved?.metadata.nullable ?? null,
      owner: resolved?.metadata.owner ?? null,
    });
  }

  const payloadPaths = [];
  for (const [pathValue, sources] of closure.payloadPaths) {
    const resolved = resolveCatalogField(pathValue, catalogByPath);
    if (!resolved) {
      addBlocker(blockers, 'payload_path_missing_from_catalog', `External payload path '${pathValue}' has no exact owning catalog declaration.`, {
        path: pathValue,
        sources: Array.from(sources).sort(),
      });
    }
    payloadPaths.push({
      path: pathValue,
      sources: Array.from(sources).sort(),
      catalog_path: resolved?.declared_at ?? null,
      type: resolved?.metadata.type ?? null,
      itemType: resolved?.metadata.itemType ?? null,
      valueType: resolved?.metadata.valueType ?? null,
      nullable: resolved?.metadata.nullable ?? null,
      owner: resolved?.metadata.owner ?? null,
    });
  }

  const typedReads = [];
  for (const read of closure.typedReads) {
    const resolved = resolveCatalogField(read.path, catalogByPath);
    let status = 'passed';
    if (!read.expected) {
      status = 'unknown';
    } else if (!resolved
      || resolved.metadata.type !== read.expected.type
      || (read.expected.itemType !== undefined && resolved.metadata.itemType !== read.expected.itemType)
      || (read.expected.valueType !== undefined && resolved.metadata.valueType !== read.expected.valueType)) {
      status = 'mismatch';
      addBlocker(blockers, 'typed_expression_catalog_mismatch', `C# read of '${read.path}' does not match its owning catalog JSON type.`, {
        path: read.path,
        csharp_type: read.csharp_type,
        expected: read.expected,
        actual: resolved ? {
          type: resolved.metadata.type,
          itemType: resolved.metadata.itemType ?? null,
          valueType: resolved.metadata.valueType ?? null,
        } : null,
        location: read.location,
      });
    }
    typedReads.push({ ...read, status, catalog_path: resolved?.declared_at ?? null });
  }

  return {
    status: blockers.length === 0 ? 'passed' : 'blocked',
    context_paths: contextPaths.sort((left, right) => left.path.localeCompare(right.path)),
    payload_paths: payloadPaths.sort((left, right) => left.path.localeCompare(right.path)),
    typed_reads: typedReads,
    memory_read_assets: closure.memoryReadAssets,
    catalog_field_count: catalogByPath.size,
  };
}

function parseRuntimeProbeOutput(text) {
  const trimmed = text.trim();
  const wrapper = trimmed.match(/^<so_property>\s*([\s\S]*?)\s*<\/so_property>$/i);
  const candidate = wrapper ? wrapper[1] : trimmed;
  try {
    return JSON.parse(candidate);
  } catch {
    return null;
  }
}

function probeOutputMessage(payload, text) {
  if (common.isRecord(payload?.payload) && typeof payload.payload.message === 'string') return payload.payload.message;
  if (typeof payload?.message === 'string') return payload.message;
  return text.trim().slice(0, 512);
}

function verifyHashBoundFile(filePath, expectedHash, label, targetRoot, blockers, algorithm = 'sha256') {
  if (typeof filePath !== 'string' || !path.isAbsolute(filePath) || typeof expectedHash !== 'string') {
    addBlocker(blockers, 'runtime_probe_artifact_invalid', `${label} must have an absolute path and expected hash.`, { path: filePath ?? null });
    return null;
  }
  let actualPath;
  try {
    actualPath = fs.realpathSync(filePath);
    if (!fs.statSync(actualPath).isFile()) throw new Error('not a file');
  } catch (error) {
    addBlocker(blockers, 'runtime_probe_artifact_missing', `${label} '${filePath}' is unavailable: ${error.message}`, { path: filePath });
    return null;
  }
  if (isWithin(targetRoot, actualPath)) {
    addBlocker(blockers, 'runtime_probe_artifact_inside_target', `${label} must remain outside the target skill root.`, { path: actualPath });
    return null;
  }
  const actualHash = crypto.createHash(algorithm).update(fs.readFileSync(actualPath)).digest(algorithm === 'sha512' ? 'base64' : 'hex');
  const normalizedExpected = algorithm === 'sha512' ? expectedHash : expectedHash.toLowerCase();
  if (actualHash !== normalizedExpected) {
    addBlocker(blockers, 'runtime_probe_artifact_hash_mismatch', `${label} no longer matches its recorded hash.`, {
      path: actualPath,
      expected_sha: normalizedExpected,
      actual_sha: actualHash,
    });
    return null;
  }
  return actualPath;
}

function runtimeLimitsMatch(actual, expected) {
  return common.isRecord(actual)
    && actual.maxBytes === expected.max_bytes
    && actual.maxArrayItems === expected.max_array_items
    && actual.maxObjectProperties === expected.max_object_properties
    && actual.maxDepth === expected.max_depth;
}

function maximumJsonDepth(value, depth = 0) {
  const children = Array.isArray(value) ? value : (common.isRecord(value) ? Object.values(value) : []);
  let maximum = depth;
  for (const child of children) maximum = Math.max(maximum, maximumJsonDepth(child, depth + 1));
  return maximum;
}

function validateRuntimeCatalogProbe(resolvedSources, targetRoot, blockers) {
  const initialBlockerCount = blockers.length;
  const probe = readSourceJson(resolvedSources.runtime_probe, 'Runtime catalog probe');
  const packageValidation = readSourceJson(resolvedSources.runtime_package_validation, 'Runtime package validation report');
  if (!common.isRecord(probe) || probe.schema_version !== 'loom-skill-enhancement-runtime-catalog-probe.v1') {
    addBlocker(blockers, 'runtime_catalog_probe_invalid', 'runtime_probe must use the versioned exact-runtime catalog probe schema.');
    return null;
  }
  if (!common.isRecord(packageValidation)) {
    addBlocker(blockers, 'runtime_package_validation_invalid', 'Runtime package validation report must be a JSON object.');
    return null;
  }

  const runtime = common.isRecord(probe.runtime) ? probe.runtime : {};
  const catalog = common.isRecord(probe.catalog) ? probe.catalog : {};
  const expectedLimits = { max_bytes: 32768, max_array_items: 256, max_object_properties: 256, max_depth: 6 };
  const profile = common.isRecord(probe.provider_limit_profile) ? probe.provider_limit_profile : {};
  if (probe.status !== 'passed' || !Array.isArray(probe.blockers) || probe.blockers.length !== 0) {
    addBlocker(blockers, 'runtime_catalog_probe_blocked', 'Exact-runtime catalog probe did not pass all required checks.', { status: probe.status ?? null });
  }
  if (!Object.entries(expectedLimits).every(([key, value]) => profile[key] === value)) {
    addBlocker(blockers, 'runtime_catalog_limit_profile_mismatch', 'Probe limit profile differs from the owning catalog provider profile.', { expected: expectedLimits, actual: profile });
  }

  const probeTool = common.isRecord(probe.probe_tool) ? probe.probe_tool : {};
  if (!samePath(probeTool.path ?? '', resolvedSources.runtime_probe_tool.path)
    || probeTool.sha256 !== resolvedSources.runtime_probe_tool.sha256) {
    addBlocker(blockers, 'runtime_probe_tool_mismatch', 'Probe report is not bound to the current checked-in runtime probe helper.', {
      expected_path: resolvedSources.runtime_probe_tool.path,
      actual_path: probeTool.path ?? null,
      expected_sha256: resolvedSources.runtime_probe_tool.sha256,
      actual_sha256: probeTool.sha256 ?? null,
    });
  }
  if (isWithin(targetRoot, resolvedSources.runtime_probe.path)
    || isWithin(targetRoot, resolvedSources.runtime_package_validation.path)) {
    addBlocker(blockers, 'runtime_probe_evidence_inside_target', 'Runtime probe and package validation evidence must be external to the target skill root.');
  }

  const validationPassed = packageValidation.source === 'nuget-registration'
    && packageValidation.package_integrity === 'passed'
    && packageValidation.archive_and_manifest_validation === 'passed'
    && packageValidation.documentation_extraction_and_hash_validation === 'passed'
    && typeof packageValidation.version === 'string'
    && typeof packageValidation.rid === 'string'
    && packageValidation.package_id === `Techne.Loom.SkillOrchestrator.Runtime.${packageValidation.rid}`;
  if (!validationPassed
    || runtime.package_id !== packageValidation.package_id
    || runtime.version !== packageValidation.version
    || runtime.rid !== packageValidation.rid
    || !samePath(runtime.package_validation_path ?? '', resolvedSources.runtime_package_validation.path)
    || runtime.package_validation_sha256 !== resolvedSources.runtime_package_validation.sha256) {
    addBlocker(blockers, 'runtime_package_evidence_mismatch', 'Runtime probe identity does not match the hash-bound published package validation report.');
  }

  const packagePath = verifyHashBoundFile(
    packageValidation.package_path,
    packageValidation.package_sha512,
    'Published runtime package',
    targetRoot,
    blockers,
    'sha512');
  if (packagePath) {
    if (runtime.package_sha512 !== packageValidation.package_sha512
      || fs.statSync(packagePath).size !== packageValidation.archive_bytes) {
      addBlocker(blockers, 'runtime_package_hash_mismatch', 'Runtime package SHA-512 or archive size differs from the validated package report.');
    }
  }
  const apphostPath = verifyHashBoundFile(runtime.apphost_path, runtime.apphost_sha256, 'Runtime apphost', targetRoot, blockers);
  if (!samePath(runtime.apphost_path ?? '', packageValidation.apphost_path ?? '') || !apphostPath) {
    addBlocker(blockers, 'runtime_apphost_evidence_mismatch', 'Runtime apphost does not match the package validation report.');
  }
  const manifestPath = verifyHashBoundFile(runtime.manifest_path, runtime.manifest_sha256, 'Runtime manifest', targetRoot, blockers);
  if (!samePath(runtime.manifest_path ?? '', packageValidation.manifest_path ?? '') || !manifestPath) {
    addBlocker(blockers, 'runtime_manifest_evidence_mismatch', 'Runtime manifest does not match the package validation report.');
  }
  const guidePath = verifyHashBoundFile(runtime.guide_path, runtime.guide_sha256, 'Runtime guide', targetRoot, blockers);
  if (!guidePath
    || !samePath(runtime.guide_path ?? '', packageValidation.guide_path ?? '')
    || !samePath(runtime.guide_path ?? '', resolvedSources.runtime_guide.path)
    || runtime.guide_sha256 !== resolvedSources.runtime_guide.sha256) {
    addBlocker(blockers, 'runtime_guide_evidence_mismatch', 'Runtime guide path or hash does not match the package and source-file evidence.');
  }

  if (!samePath(catalog.contract_path ?? '', resolvedSources.owning_context_catalog.path)
    || catalog.contract_sha256 !== resolvedSources.owning_context_catalog.sha256
    || catalog.json_pointer !== '/context_fields') {
    addBlocker(blockers, 'runtime_probe_catalog_mismatch', 'Exact-runtime probe does not read the current owning contract /context_fields fragment.', {
      expected_path: resolvedSources.owning_context_catalog.path,
      actual_path: catalog.contract_path ?? null,
      expected_sha256: resolvedSources.owning_context_catalog.sha256,
      actual_sha256: catalog.contract_sha256 ?? null,
    });
  }
  if (!Number.isSafeInteger(catalog.returned_bytes)
    || catalog.returned_bytes <= 0
    || catalog.returned_bytes > expectedLimits.max_bytes
    || !runtimeLimitsMatch(catalog.limits, expectedLimits)) {
    addBlocker(blockers, 'runtime_catalog_probe_response_mismatch', 'Runtime-reported catalog bytes or limits are invalid or exceed the provider limit.', {
      returned_bytes: catalog.returned_bytes ?? null,
      limits: catalog.limits ?? null,
    });
  }

  const requiredInvocationNames = [
    'runtime-guide',
    'current-catalog',
    'bytes-exact-limit',
    'bytes-over-limit',
    'array-exact-limit',
    'array-over-limit',
    'object-exact-limit',
    'object-over-limit',
    'depth-exact-limit',
    'depth-over-limit',
  ];
  const invocationByName = new Map();
  const invocationPayloads = new Map();
  if (!Array.isArray(probe.invocations)) {
    addBlocker(blockers, 'runtime_probe_invocations_invalid', 'Probe report must contain every raw runtime invocation.');
  } else {
    for (const invocation of probe.invocations) {
      if (!common.isRecord(invocation) || typeof invocation.name !== 'string' || invocationByName.has(invocation.name)) {
        addBlocker(blockers, 'runtime_probe_invocation_invalid', 'Runtime invocation names must be unique non-empty strings.');
        continue;
      }
      invocationByName.set(invocation.name, invocation);
      if (!Number.isSafeInteger(invocation.exit_code)) addBlocker(blockers, 'runtime_probe_exit_code_invalid', `Invocation '${invocation.name}' has no process exit code.`);
      for (const streamName of ['stdout', 'stderr']) {
        const stream = invocation[streamName];
        const streamPath = verifyHashBoundFile(stream?.path, stream?.sha256, `Runtime ${invocation.name} ${streamName}`, targetRoot, blockers);
        if (streamPath && (!Number.isSafeInteger(stream.bytes) || stream.bytes !== fs.statSync(streamPath).size)) {
          addBlocker(blockers, 'runtime_probe_stream_size_mismatch', `Invocation '${invocation.name}' ${streamName} byte count is inconsistent.`, { path: stream?.path ?? null });
        }
      }
      if (invocation.name === 'current-catalog') {
        if (!samePath(invocation.contract_path ?? '', resolvedSources.owning_context_catalog.path)
          || invocation.contract_sha256 !== resolvedSources.owning_context_catalog.sha256) {
          addBlocker(blockers, 'runtime_catalog_probe_contract_mismatch', 'Current-catalog invocation is not hash-bound to the owning context catalog.');
        }
      } else if (invocation.name !== 'runtime-guide') {
        verifyHashBoundFile(invocation.contract_path, invocation.contract_sha256, `Runtime ${invocation.name} contract`, targetRoot, blockers);
      }
      const stdoutPath = common.isRecord(invocation.stdout) ? invocation.stdout.path : null;
      let text = '';
      let payload = null;
      if (typeof stdoutPath === 'string' && fs.existsSync(stdoutPath)) {
        text = fs.readFileSync(stdoutPath, 'utf8');
        payload = parseRuntimeProbeOutput(text);
      }
      if (!payload) addBlocker(blockers, 'runtime_probe_output_invalid', `Invocation '${invocation.name}' stdout is not a recognized runtime JSON response.`);
      invocationPayloads.set(invocation.name, { invocation, payload, text, message: probeOutputMessage(payload, text) });
    }
  }
  for (const name of requiredInvocationNames) {
    if (!invocationByName.has(name)) addBlocker(blockers, 'runtime_probe_invocations_incomplete', `Probe report is missing invocation '${name}'.`, { invocation: name });
  }
  if (invocationByName.size !== requiredInvocationNames.length) {
    addBlocker(blockers, 'runtime_probe_invocation_count_mismatch', 'Probe report contains unexpected or duplicate runtime invocations.', {
      expected: requiredInvocationNames.length,
      actual: invocationByName.size,
    });
  }

  const guideResult = invocationPayloads.get('runtime-guide');
  if (!guideResult
    || guideResult.invocation.exit_code !== 0
    || guideResult.payload?.version !== runtime.version
    || !samePath(guideResult.payload?.guide_path ?? '', resolvedSources.runtime_guide.path)) {
    addBlocker(blockers, 'runtime_guide_invocation_mismatch', 'Exact apphost --guide output does not match the published runtime version and guide.');
  }
  const currentResult = invocationPayloads.get('current-catalog');
  const currentPayload = currentResult?.payload;
  if (!currentResult
    || currentResult.invocation.exit_code !== 0
    || !samePath(currentPayload?.contractPath ?? '', resolvedSources.owning_context_catalog.path)
    || currentPayload?.contractSha256 !== resolvedSources.owning_context_catalog.sha256
    || currentPayload?.returnedBytes !== catalog.returned_bytes
    || !runtimeLimitsMatch(currentPayload?.limits, expectedLimits)
    || !common.isRecord(currentPayload?.fragments?.['/context_fields'])) {
    addBlocker(blockers, 'runtime_catalog_probe_response_mismatch', 'Raw exact-runtime catalog response does not match the current owning contract and probe summary.');
  }

  const boundaryDefinitions = [
    { key: 'bytes_at_limit', invocation: 'bytes-exact-limit', success: true, dimension: 'bytes', expected: expectedLimits.max_bytes },
    { key: 'bytes_over_limit', invocation: 'bytes-over-limit', success: false, dimension: 'bytes', expected: expectedLimits.max_bytes + 1, message: `exceeds max bytes ${expectedLimits.max_bytes}` },
    { key: 'array_at_limit', invocation: 'array-exact-limit', success: true, dimension: 'array', expected: expectedLimits.max_array_items },
    { key: 'array_over_limit', invocation: 'array-over-limit', success: false, dimension: 'array', expected: expectedLimits.max_array_items + 1, message: `exceeds max array items ${expectedLimits.max_array_items}` },
    { key: 'object_properties_at_limit', invocation: 'object-exact-limit', success: true, dimension: 'object', expected: expectedLimits.max_object_properties },
    { key: 'object_properties_over_limit', invocation: 'object-over-limit', success: false, dimension: 'object', expected: expectedLimits.max_object_properties + 1, message: `exceeds max object properties ${expectedLimits.max_object_properties}` },
    { key: 'depth_at_limit', invocation: 'depth-exact-limit', success: true, dimension: 'depth', expected: expectedLimits.max_depth },
    { key: 'depth_over_limit', invocation: 'depth-over-limit', success: false, dimension: 'depth', expected: expectedLimits.max_depth + 1, message: `exceeds max depth ${expectedLimits.max_depth}` },
  ];
  const boundaryStatus = {};
  for (const definition of boundaryDefinitions) {
    const boundary = common.isRecord(probe.boundaries?.[definition.key]) ? probe.boundaries[definition.key] : {};
    const result = invocationPayloads.get(definition.invocation);
    let passed = boundary.status === 'passed' && boundary.invocation === definition.invocation && Boolean(result);
    if (!passed) addBlocker(blockers, 'runtime_catalog_boundary_evidence_invalid', `Boundary '${definition.key}' is missing or not passed.`, { boundary: definition.key });
    let inputContract = null;
    const contractPath = result?.invocation.contract_path;
    if (typeof contractPath === 'string' && fs.existsSync(contractPath)) {
      try {
        const rawContract = fs.readFileSync(contractPath, 'utf8');
        inputContract = JSON.parse(rawContract);
        if (rawContract !== JSON.stringify(inputContract)) {
          passed = false;
          addBlocker(blockers, 'runtime_probe_contract_not_compact', `Boundary '${definition.key}' input must preserve the exact compact fragment bytes.`, { path: contractPath });
        }
      } catch (error) {
        passed = false;
        addBlocker(blockers, 'runtime_probe_contract_invalid', `Boundary '${definition.key}' input is invalid JSON: ${error.message}`, { path: contractPath });
      }
    }
    const contextFields = common.isRecord(inputContract?.context_fields) ? inputContract.context_fields : null;
    const measuredBytes = contextFields ? Buffer.byteLength(JSON.stringify(contextFields), 'utf8') : null;
    let measuredDimension = null;
    if (contextFields) {
      if (definition.dimension === 'bytes') measuredDimension = measuredBytes;
      else if (definition.dimension === 'array') measuredDimension = Array.isArray(contextFields.fields) ? contextFields.fields.length : null;
      else if (definition.dimension === 'object') measuredDimension = Object.keys(contextFields).length;
      else measuredDimension = maximumJsonDepth(contextFields);
    }
    if (measuredDimension !== definition.expected) {
      passed = false;
      addBlocker(blockers, 'runtime_catalog_boundary_input_mismatch', `Boundary '${definition.key}' input does not sit at the expected limit.`, {
        expected: definition.expected,
        actual: measuredDimension,
      });
    }
    if (definition.success) {
      const payload = result?.payload;
      if (result?.invocation.exit_code !== 0
        || payload?.returnedBytes !== measuredBytes
        || !runtimeLimitsMatch(payload?.limits, expectedLimits)
        || boundary.expected_bytes !== measuredBytes
        || boundary.actual_bytes !== measuredBytes) {
        passed = false;
        addBlocker(blockers, 'runtime_catalog_boundary_response_mismatch', `Runtime did not accept the exact '${definition.dimension}' boundary.`, { boundary: definition.key });
      }
      if (definition.dimension === 'array' && boundary.expected_items !== definition.expected) passed = false;
      if (definition.dimension === 'object' && boundary.expected_properties !== definition.expected) passed = false;
      if (definition.dimension === 'depth' && boundary.expected_leaf_depth !== definition.expected) passed = false;
    } else {
      const actualMessage = result?.message ?? '';
      const expectedMessage = definition.message;
      const expectedReportMessage = boundary.expected_message;
      if (result?.invocation.exit_code === 0
        || !actualMessage.includes(expectedMessage)
        || boundary.actual_message !== actualMessage
        || expectedReportMessage !== expectedMessage
        || (definition.dimension === 'array' && boundary.attempted_items !== definition.expected)
        || (definition.dimension === 'object' && boundary.attempted_properties !== definition.expected)
        || (definition.dimension === 'bytes' && measuredBytes !== definition.expected)
        || (definition.dimension === 'depth' && boundary.attempted_leaf_depth !== definition.expected)) {
        passed = false;
        addBlocker(blockers, 'runtime_catalog_boundary_response_mismatch', `Runtime did not reject the expected over-limit '${definition.dimension}' boundary.`, { boundary: definition.key, actual_message: actualMessage });
      }
    }
    boundaryStatus[definition.key] = passed ? 'passed' : 'failed';
  }

  return {
    status: blockers.length === initialBlockerCount ? 'passed' : 'blocked',
    runtime_version: runtime.version ?? null,
    package_id: runtime.package_id ?? null,
    rid: runtime.rid ?? null,
    contract_path: catalog.contract_path ?? null,
    contract_sha256: catalog.contract_sha256 ?? null,
    json_pointer: catalog.json_pointer ?? null,
    returned_bytes: catalog.returned_bytes ?? null,
    limits: profile,
    boundary_checks: boundaryStatus,
    evidence: {
      runtime_probe: { path: resolvedSources.runtime_probe.path, sha256: resolvedSources.runtime_probe.sha256 },
      package_validation: { path: resolvedSources.runtime_package_validation.path, sha256: resolvedSources.runtime_package_validation.sha256 },
      probe_tool: { path: resolvedSources.runtime_probe_tool.path, sha256: resolvedSources.runtime_probe_tool.sha256 },
    },
  };
}

function buildDesignReport(input, snapshot, baselineReference, blockers) {
  const roots = { repositoryRoot: path.resolve(input.repository_root), targetRoot: path.resolve(input.target_skill_root) };
  const sourceSpecs = input.source_files;
  if (!common.isRecord(sourceSpecs)) fail('design phase requires source_files.');
  for (const key of Object.keys(sourceSpecs)) {
    if (!sourceFileNames.has(key)) fail(`Unknown source_files key '${key}'.`);
  }
  const resolvedSources = {};
  for (const key of sourceFileNames) {
    const spec = sourceSpecs[key];
    if (key === 'context_file' && (spec === undefined || spec === null)) {
      resolvedSources[key] = null;
      continue;
    }
    if (spec === undefined || spec === null) fail(`design phase requires source_files.${key}.`);
    resolvedSources[key] = resolveSourceFile(spec, roots, `source_files.${key}`);
  }

  if (samePath(resolvedSources.business_contract.path, resolvedSources.owning_context_catalog.path)) {
    addBlocker(blockers, 'business_and_owning_contract_same_file', 'Target business contract and owning context_fields catalog must be separate files.');
  }
  if (resolvedSources.business_contract.sha256 === resolvedSources.owning_context_catalog.sha256) {
    addBlocker(blockers, 'business_and_owning_contract_identical', 'Target business contract and owning context_fields catalog have identical bytes; keep their authorities separate.');
  }

  const assetInventory = validateAssetInventory(
    input.asset_inventory,
    snapshot,
    resolvedSources,
    input.selected_route,
    blockers);

  const businessContract = readSourceJson(resolvedSources.business_contract, 'Target business contract');
  const businessContractInfo = validateBusinessContract(businessContract, blockers);
  const workflow = readSourceJson(resolvedSources.workflow, 'Workflow source');
  if (!common.isRecord(workflow) || !common.isRecord(workflow.nodes)) {
    addBlocker(blockers, 'workflow_source_invalid', 'Workflow source must contain a nodes object.');
  }
  const nodeMapText = fs.readFileSync(resolvedSources.node_to_file_map.path, 'utf8');
  if (nodeMapText.trim().length === 0) addBlocker(blockers, 'node_to_file_map_empty', 'Node-to-file map must contain readable content.');
  const documentManifest = readSourceJson(resolvedSources.document_copy_manifest, 'Document-copy manifest');
  if (!common.isRecord(documentManifest)) addBlocker(blockers, 'document_manifest_invalid', 'Document-copy manifest must be a JSON object.');
  const contextFile = resolvedSources.context_file ? readSourceJson(resolvedSources.context_file, 'Context file') : {};
  if (!common.isRecord(contextFile)) addBlocker(blockers, 'context_file_invalid', 'Context file must be a JSON object when supplied.');

  const catalog = readSourceJson(resolvedSources.owning_context_catalog, 'Owning context catalog');
  const contextFields = common.isRecord(catalog?.context_fields) ? catalog.context_fields : null;
  if (!contextFields || contextFields.schemaVersion !== 'loom.context-fields.v1' || !Array.isArray(contextFields.fields) || contextFields.fields.length === 0) {
    addBlocker(blockers, 'owning_context_catalog_invalid', 'Owning context catalog must contain a non-empty versioned context_fields.fields array.');
  }
  const catalogFields = Array.isArray(contextFields?.fields) ? contextFields.fields : [];
  const authority = validateFieldAuthority(catalogFields, input.authoritative_fields, resolvedSources, blockers);

  const route = input.selected_route;
  if (typeof route !== 'string' || route.trim().length === 0) {
    addBlocker(blockers, 'selected_route_missing', 'selected_route must name the route used to derive the target inventory.');
  }
  const closure = common.isRecord(workflow?.nodes)
    ? collectWorkflowClosure(workflow, common.isRecord(contextFile) ? contextFile : {}, input, roots.targetRoot, assetInventory, blockers)
    : { contextPaths: new Map(), payloadPaths: new Map(), typedReads: [], memoryReadAssets: [] };
  const closureReport = validateCatalogClosure(closure, authority.catalogByPath, blockers);

  const runtimeCatalogProbe = validateRuntimeCatalogProbe(resolvedSources, roots.targetRoot, blockers);
  const sourceReport = {};
  for (const key of sourceFileNames) sourceReport[key] = resolvedSources[key];
  const businessInfo = {
    path: resolvedSources.business_contract.path,
    sha256: resolvedSources.business_contract.sha256,
    name: businessContractInfo?.name ?? null,
    role: 'target_business_contract',
    separate_from_owning_context_catalog: !samePath(resolvedSources.business_contract.path, resolvedSources.owning_context_catalog.path)
      && resolvedSources.business_contract.sha256 !== resolvedSources.owning_context_catalog.sha256,
  };
  const catalogInfo = {
    path: resolvedSources.owning_context_catalog.path,
    sha256: resolvedSources.owning_context_catalog.sha256,
    name: typeof catalog?.name === 'string' ? catalog.name : null,
    schema_version: contextFields?.schemaVersion ?? null,
    field_count: catalogFields.length,
  };
  return {
    baseline_report: baselineReference,
    baseline_snapshot: snapshot,
    source_files: sourceReport,
    target_business_contract: businessInfo,
    owning_context_catalog: catalogInfo,
    selected_route: route ?? null,
    asset_inventory: assetInventory,
    context_path_closure: closureReport,
    runtime_catalog_probe: runtimeCatalogProbe,
  };
}

function createReport(phase, status, blockers, fields) {
  const report = {
    schema_version: reportSchema,
    phase,
    status,
    blockers,
    ...fields,
  };
  report.canonical_report_sha256 = common.hashCanonical(report);
  return report;
}

function main() {
  const args = common.parseArgs(process.argv.slice(2), ['--help']);
  common.assertKnownArgs(args, ['--input-file', '--report-file', '--help']);
  if (args['--help']) {
    process.stdout.write(`${usage}\n`);
    return 0;
  }
  const inputPath = common.resolveExistingFile(common.requireOption(args, '--input-file'), 'Preflight input');
  const reportPath = common.resolveDestination(common.requireOption(args, '--report-file'), 'Preflight report');
  common.assertDistinctPaths([['preflight input', inputPath], ['preflight report', reportPath]]);
  const { value: input } = common.readJsonFile(inputPath, 'Preflight input');
  if (!common.isRecord(input) || input.schema_version !== inputSchema) {
    fail(`Preflight input must use schema_version '${inputSchema}'.`);
  }
  if (!['baseline', 'design'].includes(input.phase)) fail("phase must be 'baseline' or 'design'.");

  const blockers = [];
  const snapshot = captureBaseline(input, blockers);
  if (isWithin(snapshot.target_skill_root.replace(/\//g, path.sep), path.resolve(reportPath))) {
    fail('Preflight report must be outside the target skill root.');
  }
  let fields;
  if (input.phase === 'baseline') {
    fields = {
      repository: {
        root: snapshot.repository_root,
        head: snapshot.head,
        branch: snapshot.branch,
        requested_ref: snapshot.requested_ref,
        requested_ref_head: snapshot.requested_ref_head,
      },
      target: {
        root: snapshot.target_skill_root,
        relative_path: snapshot.target_relative_path,
        git_status: snapshot.target_git_status,
        inventory: snapshot.target_inventory,
        inventory_sha256: snapshot.target_inventory_sha256,
      },
      baseline_snapshot: snapshot,
      baseline_snapshot_sha256: common.hashCanonical(snapshot),
    };
  } else {
    const baselineReference = validateBaselineReport(input.baseline_report, snapshot, blockers);
    const designFields = buildDesignReport(input, snapshot, baselineReference, blockers);
    fields = {
      repository: {
        root: snapshot.repository_root,
        head: snapshot.head,
        branch: snapshot.branch,
        requested_ref: snapshot.requested_ref,
        requested_ref_head: snapshot.requested_ref_head,
      },
      target: {
        root: snapshot.target_skill_root,
        relative_path: snapshot.target_relative_path,
        git_status: snapshot.target_git_status,
        inventory_sha256: snapshot.target_inventory_sha256,
      },
      ...designFields,
    };
  }
  const report = createReport(input.phase, blockers.length === 0 ? 'passed' : 'blocked', blockers, fields);
  common.writeJsonNoOverwrite(reportPath, report);
  process.stdout.write(`${JSON.stringify({ status: report.status, phase: report.phase, report: normalizedAbsolute(reportPath), report_sha256: common.hashFile(reportPath), blockers: report.blockers.length })}\n`);
  return report.status === 'passed' ? 0 : 2;
}

try {
  const exitCode = main();
  if (typeof exitCode === 'number') process.exitCode = exitCode;
} catch (error) {
  process.stderr.write(`Preflight failed: ${error.message}\n`);
  process.exitCode = 2;
}
