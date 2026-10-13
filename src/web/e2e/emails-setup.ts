import { execFileSync } from 'node:child_process';
import { existsSync, readdirSync, statSync } from 'node:fs';
import { join, resolve } from 'node:path';

/**
 * Spec 094: before the browser tests run, the catalogue of emails is exported from the server's
 * unit tests as they are already built, so that the pages opened are the emails of the code as
 * it is. It is not built here — a running local API holds the modules' files — so when the
 * built tests are missing or older than the code that makes an email, this stops with one line
 * saying what to run.
 */
const here = __dirname;
const server = resolve(here, '../../server');
export const EMAILS = join(here, '.emails');

const BUILD_FIRST = 'build the server first: dotnet build src/server/Scs.sln';

/** Every source file under a folder, newest change last looked at. */
function newest(folder: string, wanted: (file: string) => boolean): number {
  let latest = 0;
  if (!existsSync(folder)) {
    return latest;
  }
  for (const entry of readdirSync(folder, { withFileTypes: true })) {
    const path = join(folder, entry.name);
    if (entry.isDirectory()) {
      if (entry.name !== 'bin' && entry.name !== 'obj') {
        latest = Math.max(latest, newest(path, wanted));
      }
    } else if (entry.name.endsWith('.cs') && wanted(path)) {
      latest = Math.max(latest, statSync(path).mtimeMs);
    }
  }
  return latest;
}

export default function exportEmails(): void {
  const built = join(server, 'tests/Scs.UnitTests/bin/Debug/net10.0/Scs.UnitTests.dll');
  if (!existsSync(built)) {
    throw new Error(`The emails cannot be exported: the server's tests are not built — ${BUILD_FIRST}`);
  }

  // Each part of the emails' code against the built file it ends up in: a build writes again
  // only the files whose own code changed, so the tests' file alone says too little.
  const bin = join(server, 'tests/Scs.UnitTests/bin/Debug/net10.0');
  const builtAt = (name: string) => (existsSync(join(bin, name)) ? statSync(join(bin, name)).mtimeMs : 0);
  const pairs: [changed: number, built: number][] = [
    [newest(join(server, 'shared/Scs.BuildingBlocks/Mail'), () => true), builtAt('Scs.BuildingBlocks.dll')],
    [newest(join(server, 'tests/Scs.UnitTests/Mail'), () => true), builtAt('Scs.UnitTests.dll')],
    ...['Identity', 'Notifications', 'Operators'].map((module): [number, number] => [
      newest(join(server, 'modules', module), (file) => /Email/i.test(file)),
      builtAt(`Scs.${module}.dll`),
    ]),
  ];
  if (pairs.some(([changed, at]) => changed > at)) {
    throw new Error(`The emails cannot be exported: the built tests are older than the emails' code — ${BUILD_FIRST}`);
  }

  execFileSync(
    'dotnet',
    ['test', join(server, 'tests/Scs.UnitTests'), '--no-build', '--nologo', '--filter', 'FullyQualifiedName~EmailCatalogueExport'],
    { env: { ...process.env, SCS_EMAILS_OUT: EMAILS }, stdio: 'pipe' },
  );

  if (!existsSync(join(EMAILS, 'catalogue.json'))) {
    throw new Error('The emails were not exported: no catalogue was written.');
  }
}
