// Spec 094: exports the catalogue of every email the platform can send and opens its index.
// Sample values only; nothing is sent. The server's tests must be built: this does not build
// them, since a running local API holds the modules' files.
import { execFileSync, spawn } from 'node:child_process';
import { existsSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const web = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const tests = resolve(web, '../server/tests/Scs.UnitTests');
const out = join(web, 'e2e/.emails');

if (!existsSync(join(tests, 'bin/Debug/net10.0/Scs.UnitTests.dll'))) {
  console.error('The emails cannot be exported: build the server first: dotnet build src/server/Scs.sln');
  process.exit(1);
}

execFileSync('dotnet', ['test', tests, '--no-build', '--nologo', '--filter', 'FullyQualifiedName~EmailCatalogueExport'], {
  env: { ...process.env, SCS_EMAILS_OUT: out },
  stdio: 'inherit',
});

const index = join(out, 'index.html');
if (!existsSync(index)) {
  console.error('The emails were not exported: the built tests are older than the code — build the server first: dotnet build src/server/Scs.sln');
  process.exit(1);
}
console.log(`The catalogue is at ${index}`);
if (!process.argv.includes('--no-open')) {
  const opener = process.platform === 'win32' ? ['cmd', ['/c', 'start', '', index]] : process.platform === 'darwin' ? ['open', [index]] : ['xdg-open', [index]];
  spawn(opener[0], opener[1], { detached: true, stdio: 'ignore' }).unref();
}
