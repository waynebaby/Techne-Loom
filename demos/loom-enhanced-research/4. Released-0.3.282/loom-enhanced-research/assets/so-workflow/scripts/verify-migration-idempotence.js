#!/usr/bin/env node
'use strict';

const fs = require('fs');
const path = require('path');
const common = require('./migration-common');

const usage = `Usage: node verify-migration-idempotence.js --workflow-file <path> --first-candidate-file <path> --second-candidate-file <path> --first-manifest-file <path> --second-manifest-file <path> [--dry-run]

Verifies two independently produced migration candidates and manifests have identical canonical content and preserve the source hash.
Existing destinations are rejected and the source is never overwritten.`;

function compareFiles(firstPath, secondPath, label) {
  const firstHash = common.hashFile(firstPath);
  const secondHash = common.hashFile(secondPath);
  return {
    label,
    first_sha256: firstHash,
    second_sha256: secondHash,
    status: firstHash === secondHash ? 'passed' : 'failed',
  };
}

common.finish(() => {
  const args = common.parseArgs(process.argv.slice(2), ['--dry-run', '--help']);
  common.assertKnownArgs(args, ['--workflow-file', '--input-file', '--first-candidate-file', '--second-candidate-file', '--first-manifest-file', '--second-manifest-file', '--report-file', '--dry-run', '--help']);
  if (args['--help']) {
    process.stdout.write(`${usage}\n`);
    return 0;
  }

  const inputPath = common.resolveExistingFile(common.requireOneOf(args, ['--workflow-file', '--input-file']), 'Workflow input');
  const firstCandidate = common.resolveExistingFile(common.requireOption(args, '--first-candidate-file'), 'First candidate');
  const secondCandidate = common.resolveExistingFile(common.requireOption(args, '--second-candidate-file'), 'Second candidate');
  const firstManifest = common.resolveExistingFile(common.requireOption(args, '--first-manifest-file'), 'First manifest');
  const secondManifest = common.resolveExistingFile(common.requireOption(args, '--second-manifest-file'), 'Second manifest');
  const reportPath = args['--report-file'] ? common.resolveDestination(args['--report-file'], 'Idempotence report') : null;
  const entries = [
    ['workflow input', inputPath],
    ['first candidate', firstCandidate],
    ['second candidate', secondCandidate],
    ['first manifest', firstManifest],
    ['second manifest', secondManifest],
  ];
  if (reportPath) entries.push(['idempotence report', reportPath]);
  common.assertDistinctPaths(entries);

  const sourceHash = common.hashFile(inputPath);
  const firstCandidateJson = common.readJsonFile(firstCandidate, 'First candidate').value;
  const secondCandidateJson = common.readJsonFile(secondCandidate, 'Second candidate').value;
  const firstManifestJson = common.readJsonFile(firstManifest, 'First manifest').value;
  const secondManifestJson = common.readJsonFile(secondManifest, 'Second manifest').value;
  const checks = [
    { check: 'source_hash_consistent', status: sourceHash === firstManifestJson.source?.sha256 && sourceHash === secondManifestJson.source?.sha256 ? 'passed' : 'failed' },
    { check: 'candidate_hash_equal', ...compareFiles(firstCandidate, secondCandidate, 'candidate') },
    { check: 'candidate_canonical_equal', status: common.canonicalJson(firstCandidateJson) === common.canonicalJson(secondCandidateJson) ? 'passed' : 'failed' },
    { check: 'manifest_projection_equal', status: common.canonicalJson(common.manifestProjection(firstManifestJson)) === common.canonicalJson(common.manifestProjection(secondManifestJson)) ? 'passed' : 'failed' },
    { check: 'source_untouched', status: firstManifestJson.source?.untouched === true && secondManifestJson.source?.untouched === true ? 'passed' : 'failed' },
    { check: 'candidate_written', status: firstManifestJson.candidate?.status === 'written' && secondManifestJson.candidate?.status === 'written' ? 'passed' : 'failed' },
  ];
  const report = {
    schema_version: 'so-migration-idempotence.v1',
    mode: args['--dry-run'] ? 'dry-run' : 'verify',
    source: { path: inputPath, sha256: sourceHash, untouched: true },
    candidates: [
      { path: firstCandidate, sha256: common.hashFile(firstCandidate) },
      { path: secondCandidate, sha256: common.hashFile(secondCandidate) },
    ],
    manifests: [
      { path: firstManifest, sha256: common.hashFile(firstManifest) },
      { path: secondManifest, sha256: common.hashFile(secondManifest) },
    ],
    checks,
    status: checks.every(check => check.status === 'passed') ? 'passed' : 'failed',
  };
  report.canonical_report_sha256 = common.hashCanonical(common.manifestProjection(report));
  if (reportPath) {
    common.writeJsonNoOverwrite(reportPath, report);
  }
  process.stdout.write(`${JSON.stringify({ status: report.status, report: reportPath, checks: checks.length })}\n`);
  return report.status === 'passed' ? 0 : 2;
});
