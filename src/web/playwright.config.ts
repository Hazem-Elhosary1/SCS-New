import { defineConfig, devices } from '@playwright/test';

// Browsers are kept inside node_modules (install with:
//   PLAYWRIGHT_BROWSERS_PATH=0 npx playwright install chromium), so nothing is written to the
// user profile.
process.env['PLAYWRIGHT_BROWSERS_PATH'] ??= '0';

const apps = [
  { name: 'clinic', port: 4201 },
  { name: 'patient', port: 4202 },
  { name: 'ops', port: 4203 },
];

/**
 * Browser checks of the conventions every screen inherits (quickstart.md Scenario 5, SC-012).
 * The platform is answered by the tests themselves, so only the three front ends are started.
 */
export default defineConfig({
  testDir: './e2e',
  // The catalogue of emails is exported from the server's built tests before anything runs (spec 094).
  globalSetup: './e2e/emails-setup.ts',
  fullyParallel: true,
  forbidOnly: !!process.env['CI'],
  retries: process.env['CI'] ? 1 : 0,
  reporter: [['list']],
  // The gallery's kept pictures, one per specimen (spec 095, research R7).
  snapshotPathTemplate: '{testDir}/gallery/__pictures__/{arg}{ext}',
  // With `SCS_SHOTS` set, every test also leaves a picture of its last screen in its results folder
  // (spec 095: the clinic's and patients' screens pictured before and after a change of shared parts).
  use: {
    ...devices['Desktop Chrome'],
    trace: 'retain-on-failure',
    screenshot: process.env['SCS_SHOTS'] ? 'on' : 'off',
    // Pictures taken for comparing are taken still: the screens honour "reduce motion".
    ...(process.env['SCS_SHOTS'] ? { contextOptions: { reducedMotion: 'reduce' as const } } : {}),
  },
  webServer: apps.map((app) => ({
    command: `npx ng serve ${app.name}`,
    url: `http://localhost:${app.port}`,
    reuseExistingServer: !process.env['CI'],
    timeout: 180_000,
  })),
});
