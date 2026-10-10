// Writes version.json into each app's build output (contracts/frontend-runtime.md §2).
// Run after `ng build`. The pipeline sets SCS_VERSION, SCS_COMMIT and SCS_BUILT_AT so the front
// ends and the API report the same release; a local build falls back to the working copy.
import { execFileSync } from 'node:child_process';
import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const workspace = join(dirname(fileURLToPath(import.meta.url)), '..');
const apps = ['clinic', 'patient', 'ops'];

function localCommit() {
  try {
    return execFileSync('git', ['rev-parse', 'HEAD'], { cwd: workspace, encoding: 'utf8' }).trim();
  } catch {
    return '0'.repeat(40);
  }
}

const commit = process.env.SCS_COMMIT || localCommit();
const base = JSON.parse(readFileSync(join(workspace, 'package.json'), 'utf8')).version;
const version = process.env.SCS_VERSION || `${base}+${commit.slice(0, 7)}`;
const builtAt = process.env.SCS_BUILT_AT || new Date().toISOString();

if (!/^[0-9a-f]{40}$/.test(commit)) {
  console.error('SCS_COMMIT must be a 40-character commit id.');
  process.exit(1);
}

let missing = false;
for (const app of apps) {
  const output = join(workspace, 'dist', app, 'browser');
  if (!existsSync(output)) {
    console.error(`No build output for "${app}" at ${output}. Build the app first.`);
    missing = true;
    continue;
  }
  writeFileSync(
    join(output, 'version.json'),
    JSON.stringify({ app, version, commit, builtAt }, null, 2) + '\n',
  );
  console.log(`${app}: ${version}`);
}

process.exit(missing ? 1 : 0);
