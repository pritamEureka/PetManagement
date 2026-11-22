// Playwright config. @playwright/test is installed from frontend/e2e/package.json (NOT the main package.json):
//   cd frontend/e2e && npm install && npm run install:browsers && npm test
// The main tsconfig only includes src/, so this file and e2e/ do not affect `npm run build`.
import { defineConfig, devices } from "@playwright/test";

const baseURL = process.env.E2E_BASE_URL ?? "http://localhost:5173";
// Set E2E_NO_WEBSERVER=1 when the app is already running (e.g. docker compose web on :3000).
const useWebServer = !process.env.E2E_NO_WEBSERVER;

export default defineConfig({
  testDir: ".",
  testMatch: /.*.e2e.ts$/,
  timeout: 60_000,
  expect: { timeout: 10_000 },
  fullyParallel: false,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [["github"], ["html", { open: "never" }]] : "list",
  use: { baseURL, trace: "retain-on-failure", screenshot: "only-on-failure" },
  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],
  webServer: useWebServer
    ? {
        cwd: "..",
        command: "npm run dev -- --host 127.0.0.1 --port 5173",
        url: "http://localhost:5173",
        reuseExistingServer: !process.env.CI,
        timeout: 120_000,
      }
    : undefined,
});
