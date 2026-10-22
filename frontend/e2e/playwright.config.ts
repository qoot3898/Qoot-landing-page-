import { defineConfig, devices } from '@playwright/test';

/**
 * End-to-end tests against a running Qoot API that serves the built website (npm run build, then
 * start the API with Frontend__DistPath pointing at frontend/dist).
 *
 * They create, publish and delete real content: run them against a development database only.
 *   QOOT_E2E_URL        default http://localhost:5080
 *   QOOT_E2E_EMAIL      an administrator account
 *   QOOT_E2E_PASSWORD   its password
 */
export default defineConfig({
  testDir: '.',
  timeout: 60_000,
  fullyParallel: false,
  workers: 1,
  retries: 0,
  reporter: [['list']],
  use: {
    baseURL: process.env.QOOT_E2E_URL ?? 'http://localhost:5080',
    trace: 'retain-on-failure',
    ...devices['Desktop Chrome'],
    launchOptions: process.env.QOOT_E2E_CHROMIUM ? { executablePath: process.env.QOOT_E2E_CHROMIUM, args: ['--no-sandbox'] } : {},
  },
});
