// The browser smoke test across the merged slices, run in CI against the compose stack:
// switch user, upload a small synthetic export, read its report, then decide one conflict
// from two tabs at once. Each run uses its own vote id, so it repeats on the same stack.
// With the stack up (see stack.mjs):  npm --prefix e2e test
import assert from 'node:assert/strict';
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { after, before, test } from 'node:test';
import { chromium } from 'playwright';
import { actAs, BASE_URL, FIXTURES } from './stack.mjs';

const run = Date.now().toString(36);
const workDir = mkdtempSync(path.join(tmpdir(), 'meridian-smoke-'));
let browser;

before(async () => {
  browser = await chromium.launch();
});

after(async () => {
  await browser?.close();
  rmSync(workDir, { recursive: true, force: true });
});

/** The committed fixture with this run's vote id, under its own file name. */
function fixture(fileName) {
  const file = path.join(workDir, fileName);
  writeFileSync(file, readFileSync(path.join(FIXTURES, fileName), 'utf8').replaceAll('{run}', run));
  return file;
}

/** Uploads through the Imports page and waits on the report until its job is done. */
async function upload(page, fileName) {
  await page.goto(`${BASE_URL}/imports`);
  await page.locator('input[type=file][accept*=csv]').setInputFiles(fixture(fileName));
  await page.waitForURL(/\/imports\/\d+$/);
  await page.getByTestId('outcome-filters').waitFor({ timeout: 60_000 });
}

/** The count the report's outcome filter shows for `outcome`. */
async function outcomeCount(page, outcome) {
  return Number(await page.locator(`[data-outcome=${outcome}] strong`).innerText());
}

test('switch user, upload, report, and a decision race in two tabs', async () => {
  const context = await browser.newContext();
  const omar = await context.newPage();
  await omar.goto(`${BASE_URL}/imports`);
  await omar.getByTestId('user-switcher').click();
  await omar.getByRole('menuitem', { name: /Omar/ }).click();
  await assert.doesNotReject(omar.getByTestId('user-switcher').filter({ hasText: 'Omar' }).waitFor());

  await upload(omar, 'smoke_export.csv');
  await omar.getByText(/Uploaded by omar/i).waitFor();
  assert.equal(await outcomeCount(omar, 'new'), 3);
  assert.equal(await outcomeCount(omar, 'duplicate'), 1);
  assert.equal(await outcomeCount(omar, 'invalid'), 1);

  await upload(omar, 'smoke_export_rev.csv');
  assert.equal(await outcomeCount(omar, 'unchanged'), 2);
  assert.equal(await outcomeCount(omar, 'conflict'), 1);
  const opened = omar.getByTestId('conflicts-opened').getByRole('link');
  assert.equal(await opened.count(), 1);
  const conflictUrl = new URL(await opened.getAttribute('href'), BASE_URL).href;

  // A second tab acts as Dana; both analysts open the pending conflict before either decides.
  const dana = await context.newPage();
  await actAs(dana, 'dana');
  await Promise.all([omar.goto(conflictUrl), dana.goto(conflictUrl)]);
  for (const page of [omar, dana]) await page.getByRole('button', { name: 'Accept incoming' }).waitFor();

  await dana.getByRole('button', { name: 'Accept incoming' }).click();
  await dana.getByTestId('decision').waitFor();
  await omar.getByRole('button', { name: 'Keep current' }).click();

  const lost = omar.getByTestId('already-decided');
  await lost.waitFor();
  assert.match(await lost.innerText(), /Someone else decided first/);
  assert.match(await lost.innerText(), /accepted/i);
  assert.equal(await omar.getByRole('button', { name: 'Keep current' }).count(), 0);

  await context.close();
});
