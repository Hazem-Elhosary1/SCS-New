// Translation check, part of the pull-request gate (SH-002, contracts/frontend-runtime.md §3).
// It fails when:
//   1. a key exists in one of ar.json / en.json and not the other;
//   2. a value is empty;
//   3. a template contains visible text written directly instead of through a key;
//   4. a template uses a key that is in no translation file it can load.
//
//   node tools/i18n-check.mjs [workspace-directory]
import { existsSync, readFileSync, readdirSync, statSync } from 'node:fs';
import { join, relative, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

const LETTER = /[A-Za-z؀-ۿ]/;
const TEXT_ATTRIBUTES = ['aria-label', 'placeholder', 'title', 'alt', 'label'];
const CONTROL_FLOW =
  /@(?:else\s+if|if|else|for|empty|switch|case|default|defer|placeholder|loading|error)\b[^{}]*\{/g;

export function flatten(value, prefix = '', out = {}) {
  for (const [key, child] of Object.entries(value ?? {})) {
    const path = prefix ? `${prefix}.${key}` : key;
    if (child !== null && typeof child === 'object' && !Array.isArray(child)) {
      flatten(child, path, out);
    } else {
      out[path] = child;
    }
  }
  return out;
}

/** Problems in one pair of translation files. */
export function comparePair(ar, en, label) {
  const problems = [];
  const arabic = flatten(ar);
  const english = flatten(en);

  for (const key of Object.keys(arabic)) {
    if (!(key in english)) problems.push(`${label}: "${key}" is in ar.json but not in en.json`);
  }
  for (const key of Object.keys(english)) {
    if (!(key in arabic)) problems.push(`${label}: "${key}" is in en.json but not in ar.json`);
  }
  for (const [language, values] of [
    ['ar', arabic],
    ['en', english],
  ]) {
    for (const [key, value] of Object.entries(values)) {
      if (typeof value !== 'string' || value.trim() === '') {
        problems.push(`${label}: "${key}" is empty in ${language}.json`);
      }
    }
  }
  return problems;
}

/** Templates in a source file: the whole file for .html, the `template:` strings for .ts. */
export function templatesOf(file, source) {
  if (file.endsWith('.html')) return [source];
  const found = [];
  for (const match of source.matchAll(/\btemplate:\s*`([\s\S]*?)`/g)) found.push(match[1]);
  for (const match of source.matchAll(/\btemplate:\s*'((?:[^'\\]|\\.)*)'/g)) found.push(match[1]);
  return found;
}

/** Visible text written directly in a template. */
export function literalText(template) {
  const found = [];

  for (const attribute of TEXT_ATTRIBUTES) {
    const pattern = new RegExp(`(?<![\\[\\w.-])${attribute}="([^"]*)"`, 'g');
    for (const match of template.matchAll(pattern)) {
      if (LETTER.test(match[1])) found.push(`${attribute}="${match[1]}"`);
    }
  }

  const text = template
    .replace(/<!--[\s\S]*?-->/g, ' ')
    .replace(/\{\{[\s\S]*?\}\}/g, ' ')
    .replace(CONTROL_FLOW, ' ')
    .replace(/<[^>]*>/g, '\n')
    .replace(/[{}]/g, ' ');

  for (const line of text.split('\n')) {
    const trimmed = line.trim();
    if (trimmed && LETTER.test(trimmed)) found.push(trimmed);
  }
  return found;
}

/** Translation keys a template uses through the transloco pipe. */
export function keysUsed(template) {
  return [...template.matchAll(/'([A-Za-z][\w.]*)'\s*\|\s*transloco/g)].map((match) => match[1]);
}

function walk(directory, files = []) {
  if (!existsSync(directory)) return files;
  for (const name of readdirSync(directory)) {
    const path = join(directory, name);
    if (statSync(path).isDirectory()) walk(path, files);
    else files.push(path);
  }
  return files;
}

function readJson(path) {
  return JSON.parse(readFileSync(path, 'utf8'));
}

export function check(workspace) {
  const problems = [];
  const projects = join(workspace, 'projects');
  const keysByProject = {};

  // 1 and 2: every pair of translation files.
  for (const project of readdirSync(projects)) {
    const candidates = [join(projects, project, 'public', 'i18n'), join(projects, project, 'i18n')];
    const directory = candidates.find((path) => existsSync(join(path, 'ar.json')));
    if (!directory) continue;

    const enFile = join(directory, 'en.json');
    if (!existsSync(enFile)) {
      problems.push(`${project}: en.json is missing next to ar.json`);
      continue;
    }
    const ar = readJson(join(directory, 'ar.json'));
    const en = readJson(enFile);
    problems.push(...comparePair(ar, en, project));
    keysByProject[project] = new Set([...Object.keys(flatten(ar)), ...Object.keys(flatten(en))]);
  }

  const shared = keysByProject['ui'] ?? new Set();
  const apps = Object.keys(keysByProject).filter((project) => project !== 'ui');

  // 3 and 4: every template.
  for (const file of walk(projects)) {
    if (
      !/\.(html|ts)$/.test(file) ||
      /\.spec\.ts$/.test(file) ||
      file.includes(`${sep}testing${sep}`)
    ) {
      continue;
    }
    const name = relative(workspace, file).split(sep).join('/');
    // The host page holds only the product name in <title>, which is not translated.
    if (/\/src\/index\.html$/.test(name)) continue;
    const project = name.split('/')[1];

    for (const template of templatesOf(file, readFileSync(file, 'utf8'))) {
      for (const text of literalText(template)) {
        problems.push(`${name}: text written directly in the template: ${text}`);
      }
      for (const key of keysUsed(template)) {
        // A shared component may use a key every app supplies (for example the app's name).
        const known =
          shared.has(key) ||
          (keysByProject[project]?.has(key) ?? false) ||
          (project in keysByProject === false || project === 'ui'
            ? apps.length > 0 && apps.every((app) => keysByProject[app].has(key))
            : false);
        if (!known) problems.push(`${name}: key "${key}" is in no translation file`);
      }
    }
  }

  return problems;
}

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
  const workspace = process.argv[2] ?? join(fileURLToPath(import.meta.url), '..', '..');
  const problems = check(workspace);
  if (problems.length > 0) {
    console.error(`Translation check failed (${problems.length}):`);
    for (const problem of problems) console.error(`  - ${problem}`);
    process.exit(1);
  }
  console.log('Translation check passed.');
}
