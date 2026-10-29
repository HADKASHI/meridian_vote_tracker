// Captures every screen of the app against the compose stack, with synthetic data only, at
// 1280 px and 390 px in light and dark, and reports any page that scrolls sideways.
// Development only, never run in CI. With the stack up (see stack.mjs):
//   node e2e/screens.mjs [output folder, default docs/screens] [themes, default light,dark]
// Exits 1 when a page scrolls sideways.
import { mkdirSync } from 'node:fs';
import path from 'node:path';
import { chromium } from 'playwright';
import { actAs, api, BASE_URL, ensureDemoData, ROOT } from './stack.mjs';

const outDir = path.resolve(ROOT, process.argv[2] ?? 'docs/screens');
const themes = (process.argv[3] ?? 'light,dark').split(',');
const widths = [
  { name: '1280', viewport: { width: 1280, height: 800 } },
  { name: '390', viewport: { width: 390, height: 844 } },
];

/** The ids the screens need, read from the API so the script follows whatever the seed made. */
async function findIds() {
  const imports = (await api('/api/imports?pageSize=50')).items;
  const original = imports.find((i) => i.fileName === 'demo_export_2024_q2.csv');
  const revision = imports.find((i) => i.fileName === 'demo_export_2024_q2_rev2.csv');
  const pending = (await api('/api/conflicts?state=pending&pageSize=50')).items;
  const decided = (await api('/api/conflicts?state=decided&pageSize=50')).items;
  const longTitle = (await api('/api/votes?q=MA/2024/104&pageSize=1')).items[0];
  const snapshot = (await api('/api/snapshots?pageSize=50')).items.find((s) => s.job.state === 'done');
  const results = (await api(`/api/snapshots/${snapshot.id}`)).results;
  return {
    original: original.id,
    revision: revision.id,
    pendingConflict: pending.find((c) => c.subject === 'details')?.id ?? pending[0].id,
    decidedConflict: decided[0].id,
    decidedVote: decided[0].vote.voteId,
    vote: longTitle.id,
    snapshot: snapshot.id,
    state: results[0].stateCode,
  };
}

/** Every screen: a name, the user it is seen as, its URL, and anything to do before the capture. */
function screens(ids) {
  return [
    { name: 'imports', url: '/imports' },
    { name: 'import-report', url: `/imports/${ids.original}` },
    { name: 'import-report-revision', url: `/imports/${ids.revision}` },
    { name: 'review', url: '/review' },
    { name: 'review-conflict', url: `/review/${ids.pendingConflict}` },
    { name: 'review-decided', url: `/review/${ids.decidedConflict}?state=decided` },
    { name: 'votes', url: '/votes' },
    { name: 'vote', url: `/votes/${ids.vote}` },
    { name: 'vote-timeline', url: `/votes/${ids.decidedVote}?tab=timeline` },
    { name: 'positions', url: '/positions', user: 'lena' },
    { name: 'positions-analyst', url: '/positions' },
    { name: 'snapshots', url: '/snapshots' },
    { name: 'snapshot-new', url: '/snapshots/new' },
    { name: 'snapshot-preview', url: '/snapshots/new', act: preview },
    { name: 'snapshot', url: `/snapshots/${ids.snapshot}?state=${ids.state}`, user: 'lena' },
    { name: 'publications', url: '/publications' },
    { name: 'jobs', url: '/jobs' },
    { name: 'not-found', url: '/no-such-page' },
  ];
}

/** Opens the preview dialog for a range that holds votes without a position. */
async function preview(page) {
  await page.getByLabel('Name').fill('2024 first half');
  await page.getByLabel('From').fill('2024-01-01');
  await page.getByLabel('To').fill('2024-06-30');
  await page.getByRole('button', { name: /Preview/ }).click();
  await page.getByRole('dialog').waitFor();
}

async function settle(page) {
  await page.waitForLoadState('networkidle');
  await page.evaluate(() => document.fonts.ready);
  await page.waitForTimeout(400);
}

/** How far the page scrolls sideways, and the widest elements that cause it. */
function sidewaysOverflow(page) {
  return page.evaluate(() => {
    const width = document.documentElement.clientWidth;
    const over = document.documentElement.scrollWidth - width;
    if (over <= 0) return null;
    const culprits = [...document.querySelectorAll('body *')]
      .filter((el) => el.getBoundingClientRect().right > width + 1)
      .slice(0, 5)
      .map((el) => `${el.tagName.toLowerCase()}${el.className && typeof el.className === 'string' ? '.' + el.className.trim().split(/\s+/).join('.') : ''}`);
    return { over, culprits };
  });
}

const browser = await chromium.launch();
await ensureDemoData(browser);
const ids = await findIds();
mkdirSync(outDir, { recursive: true });
const problems = [];
for (const theme of themes) {
  for (const { name: width, viewport } of widths) {
    for (const screen of screens(ids)) {
      const context = await browser.newContext({ viewport, colorScheme: theme });
      const page = await context.newPage();
      await actAs(page, screen.user ?? 'dana');
      await page.goto(`${BASE_URL}${screen.url}`);
      await settle(page);
      if (screen.act) {
        await screen.act(page);
        await settle(page);
      }
      const overflow = await sidewaysOverflow(page);
      if (overflow) problems.push(`${screen.name} ${width} ${theme}: ${overflow.over}px sideways (${overflow.culprits.join(', ')})`);
      await page.screenshot({ path: path.join(outDir, `${screen.name}-${width}-${theme}.png`), fullPage: true });
      await context.close();
    }
  }
}
await browser.close();
console.log(`Captured ${themes.length * widths.length * screens(ids).length} screens into ${path.relative(ROOT, outDir)}.`);
if (problems.length > 0) {
  console.log(`Pages that scroll sideways:\n  ${problems.join('\n  ')}`);
  process.exitCode = 1;
}
