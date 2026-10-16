// Each app's look is declared on an attribute its page carries from the first paint (spec 095,
// research R3), so a shared component can also be drawn in another app's look inside a wrapper.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';

const projects = fileURLToPath(new URL('../projects/', import.meta.url));
const apps = ['clinic', 'patient', 'ops'];
const tokensOf = (app) => readFileSync(join(projects, app, 'src/styles/tokens.css'), 'utf8');

for (const app of apps) {
  test(`${app}: the page carries its look and the tokens are declared on it`, () => {
    const page = readFileSync(join(projects, app, 'src/index.html'), 'utf8');
    const tokens = tokensOf(app);
    assert.ok(page.includes(`data-scs-look="${app}"`) && /<html [^>]*data-scs-look=/.test(page), 'the <html> carries the look');
    assert.ok(tokens.includes(`[data-scs-look='${app}'] {`), 'the tokens are declared on the look');
    assert.doesNotMatch(tokens, /^:root/m, 'no token is declared on :root');
  });
}

/**
 * Tokens read with no fallback (`var(--scs-x)`) in the source files under a folder — leaving out the
 * settings a part declares for itself (`--scs-button-bg: …` inside the button's own rule).
 */
function readWithoutFallback(folder) {
  const found = new Set();
  const own = new Set();
  const walk = (directory) => {
    for (const name of readdirSync(directory)) {
      const path = join(directory, name);
      if (statSync(path).isDirectory()) walk(path);
      else if (/\.(ts|css|html)$/.test(name) && !name.endsWith('.spec.ts') && !path.includes(join('src', 'styles'))) {
        const text = readFileSync(path, 'utf8');
        for (const match of text.matchAll(/var\(\s*(--scs-[a-z0-9-]+)\s*\)/g)) found.add(match[1]);
        for (const match of text.matchAll(/(--scs-[a-z0-9-]+)\s*:/g)) own.add(match[1]);
      }
    }
  };
  walk(folder);
  return new Set([...found].filter((token) => !own.has(token)));
}

test('every token a shared part reads without a fallback is declared in every look', () => {
  const shared = new Set([...readWithoutFallback(join(projects, 'ui/src')), ...readWithoutFallback(join(projects, 'core/src'))]);
  for (const app of apps) {
    const tokens = tokensOf(app);
    const missing = [...shared].filter((token) => !tokens.includes(`${token}:`));
    assert.deepEqual(missing, [], `${app} lacks tokens the shared parts read`);
  }
});

for (const app of apps) {
  test(`${app}: every token its own code reads without a fallback is in its look`, () => {
    const tokens = tokensOf(app);
    const missing = [...readWithoutFallback(join(projects, app, 'src'))].filter((token) => !tokens.includes(`${token}:`));
    assert.deepEqual(missing, []);
  });
}

test("the gallery's clinic look clears every setting the operators' look has and the clinic's lacks", () => {
  const declared = (text) => new Set([...text.matchAll(/^\s*(--scs-[a-z0-9-]+):/gm)].map((match) => match[1]));
  const ops = declared(tokensOf('ops'));
  const clinic = declared(tokensOf('clinic'));
  const reset = readFileSync(join(projects, 'ops/src/app/gallery/clinic-reset.css'), 'utf8');
  const missing = [...ops].filter((name) => !clinic.has(name) && !reset.includes(`${name}: initial;`));
  assert.deepEqual(missing, [], 'add them to ops/src/app/gallery/clinic-reset.css');
});
