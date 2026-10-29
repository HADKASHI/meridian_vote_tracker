// Setup shared by the development-only browser scripts: the compose stack's address, the
// API as a seeded user, synthetic data, and a browser tab acting as a chosen user.
// Start the stack with the synthetic member states first:
//   DATA_DIR=./tests/data docker compose up -d --build --wait
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

export const BASE_URL = process.env.BASE_URL ?? 'http://127.0.0.1:8080';
export const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
export const FIXTURES = path.join(ROOT, 'e2e', 'fixtures');

/** GET or POST the API as `user` (dana, omar or lena) and return the JSON body. */
export async function api(pathAndQuery, { user = 'dana', method = 'GET', body } = {}) {
  const response = await fetch(`${BASE_URL}${pathAndQuery}`, {
    method,
    headers: { 'X-User-Id': user, ...(body ? { 'Content-Type': 'application/json' } : {}) },
    body: body ? JSON.stringify(body) : undefined,
  });
  if (!response.ok) throw new Error(`${method} ${pathAndQuery}: ${response.status} ${await response.text()}`);
  return response.status === 204 ? null : response.json();
}

/** Fills an empty database with the `seed-demo` set (Development only, so the worker image runs it as Development). */
export function seedDemo() {
  execFileSync(
    'docker',
    ['compose', 'run', '--rm', '--no-deps', '-e', 'DOTNET_ENVIRONMENT=Development', '--entrypoint', 'dotnet', 'worker',
      '/app/worker/Meridian.Worker.dll', 'seed-demo'],
    { cwd: ROOT, stdio: ['ignore', 'pipe', 'inherit'] },
  );
  console.log('Demo data seeded.');
}

/** Makes `page` act as `user`, as the header's user switcher would. */
export async function actAs(page, user) {
  await page.addInitScript((id) => {
    sessionStorage.setItem('mvt.user', id);
    localStorage.setItem('mvt.lastUser', id);
  }, user);
}

/** Uploads a fixture through the Imports page and waits until its job is done or failed; returns the import id. */
export async function uploadThroughImportsPage(page, fileName) {
  await page.goto(`${BASE_URL}/imports`);
  await page.locator('input[type=file]').setInputFiles(path.join(FIXTURES, fileName));
  await page.waitForURL(/\/imports\/\d+/);
  const importId = Number(page.url().match(/\/imports\/(\d+)/)[1]);
  for (let i = 0; i < 120; i++) {
    const report = await api(`/api/imports/${importId}`);
    if (report.job.state === 'done' || report.job.state === 'failed') return importId;
    await new Promise((resolve) => setTimeout(resolve, 500));
  }
  throw new Error(`Import #${importId} did not finish`);
}

/** The stack's synthetic data: seed-demo, then the original export and its revision through the Imports page. */
export async function ensureDemoData(browser) {
  const votes = await api('/api/votes?pageSize=1');
  if (votes.total > 0) return;
  seedDemo();
  const context = await browser.newContext();
  const page = await context.newPage();
  await actAs(page, 'omar');
  await uploadThroughImportsPage(page, 'demo_export_2024_q2.csv');
  await uploadThroughImportsPage(page, 'demo_export_2024_q2_rev2.csv');
  await context.close();
}
