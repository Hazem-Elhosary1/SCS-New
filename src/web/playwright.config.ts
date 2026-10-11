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
  fullyParallel: true,
  forbidOnly: !!process.env['CI'],
  retries: process.env['CI'] ? 1 : 0,
  reporter: [['list']],
  use: { ...devices['Desktop Chrome'], trace: 'retain-on-failure' },
  webServer: apps.map((app) => ({
    command: `npx ng serve ${app.name}`,
    url: `http://localhost:${app.port}`,
    reuseExistingServer: !process.env['CI'],
    timeout: 180_000,
  })),
});
