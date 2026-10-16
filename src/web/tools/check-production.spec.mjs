import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, mkdirSync, writeFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { galleryTraces } from './check-production.mjs';

test('a build that names the gallery is found out, file by file', () => {
  const root = mkdtempSync(join(tmpdir(), 'dist-'));
  try {
    mkdirSync(join(root, 'browser'));
    writeFileSync(join(root, 'browser', 'main.js'), 'const routes = [{ path: "sign-in" }];');
    writeFileSync(join(root, 'browser', 'index.html'), '<html data-scs-look="ops"></html>');
    assert.deepEqual(galleryTraces(root), []);

    writeFileSync(join(root, 'browser', 'chunk-1.js'), 'loadComponent: () => import("./app-gallery-page"), path: "dev/components"');
    assert.deepEqual(
      galleryTraces(root).map((trace) => `${trace.file}:${trace.mark}`).sort(),
      ['browser/chunk-1.js:app-gallery-page', 'browser/chunk-1.js:dev/components'],
    );
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});
