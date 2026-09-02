// Drives the PUBLISHED application in a real browser.
//
// Nothing else in this repository confirms the app starts. The unit tests cover the
// model, the exporter, the state service and persistence; CI builds, formats, scans and
// publishes; the Pages job checks that files are PRESENT. Not one of those loads
// index.html or boots the WebAssembly runtime.
//
// That gap is not theoretical. Two of the five defects this roadmap fixed — a chart title
// painted in its own background colour, and phase bands misplaced by a factor of five —
// were invisible to every automated check and were found by looking at the page.
//
// Each check below guards something that was previously verified only by reading:
//
//   1 boots            nothing has ever asserted this
//   2 chart renders    the ONLY exercise of the SRI hashes added in #4
//   3 title legible    #8, as a regression test rather than a screenshot
//   4 bands placed     #9, likewise
//   5 row identity     #13's acceptance criterion, which needed a real DOM
//   6 persistence      #14's reload, likewise
//   7 deep links       #15's 404.html fallback under the published base path
//
// Every check runs and reports independently rather than aborting at the first failure.
// That is deliberate: when something breaks, the useful output is which checks noticed.

import { chromium } from 'playwright';
import { mkdirSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

const BASE = process.argv[2] || 'http://localhost:8080/SupercompensationApp/';
// Resolved against this file rather than the working directory, so the workflow can
// invoke it from anywhere without silently writing the screenshot somewhere nobody
// uploads from.
const ARTIFACTS = fileURLToPath(new URL('./artifacts', import.meta.url));
const BOOT_TIMEOUT = 90_000;

const results = [];
const record = (name, ok, detail) => {
    results.push({ name, ok, detail });
    console.log(`${ok ? 'PASS' : 'FAIL'}  ${name}${detail ? `\n        ${detail}` : ''}`);
};

/** Runs a check without letting a throw stop the rest of them. */
async function check(name, fn) {
    try {
        const detail = await fn();
        record(name, true, detail);
    } catch (err) {
        record(name, false, String(err && err.message ? err.message : err).split('\n')[0]);
    }
}

const assert = (cond, msg) => {
    if (!cond) throw new Error(msg);
};

/** Both colours through the same pipe, so #f1f5f9 and rgb(241,245,249) compare equal. */
const NORMALISE_COLOUR = `(value) => {
    const probe = document.createElement('span');
    probe.style.color = value;
    document.body.appendChild(probe);
    const out = getComputedStyle(probe).color;
    probe.remove();
    return out;
}`;

mkdirSync(ARTIFACTS, { recursive: true });

const browser = await chromium.launch();
const context = await browser.newContext({ viewport: { width: 1280, height: 900 } });
const page = await context.newPage();

const consoleErrors = [];
page.on('console', (m) => { if (m.type() === 'error') consoleErrors.push(m.text()); });
page.on('pageerror', (e) => consoleErrors.push(String(e)));

// ── 1. It boots ──────────────────────────────────────────────────────────────
await check('1  the application boots', async () => {
    const response = await page.goto(BASE, { waitUntil: 'domcontentloaded' });
    assert(response && response.ok(), `index.html returned ${response && response.status()}`);
    await page.waitForSelector('h2:has-text("Parametry Sprintu")', { timeout: BOOT_TIMEOUT });
    const loading = await page.locator('text=Ładowanie aplikacji').count();
    assert(loading === 0, 'the loading placeholder is still on the page');
    return 'Konfiguracja rendered; WebAssembly runtime started';
});

// ── 2. The chart renders (and with it, the CDN scripts and their SRI hashes) ──
await check('2  the chart renders', async () => {
    assert(
        await page.evaluate(() => typeof window.Chart !== 'undefined'),
        'window.Chart is undefined — the CDN script did not execute. A wrong SRI hash ' +
        'makes the browser refuse it silently, and this is the only place that would show.');

    await page.locator('.btn-generate').click();
    await page.waitForSelector('#supercompChart', { timeout: 30_000 });

    const info = await page.waitForFunction(() => {
        const c = window.Chart.getChart('supercompChart');
        return c ? { datasets: c.data.datasets.length, points: c.data.datasets[0].data.length } : null;
    }, null, { timeout: 30_000 }).then((h) => h.jsonValue());

    assert(info.datasets >= 2, `expected performance + baseline datasets, got ${info.datasets}`);
    assert(info.points > 100, `expected a fine-grained curve, got ${info.points} points`);
    return `Chart.js live: ${info.datasets} datasets, ${info.points} points`;
});

// ── 3. The chart title is legible (#8) ───────────────────────────────────────
await check('3  the chart title is not its own background colour', async () => {
    const seen = await page.evaluate((normaliseSrc) => {
        const normalise = eval(normaliseSrc);
        const chart = window.Chart.getChart('supercompChart');
        return {
            title: normalise(chart.options.plugins.title.color),
            ticks: normalise(chart.options.scales.y.ticks.color),
            background: getComputedStyle(document.querySelector('.chart-container')).backgroundColor,
        };
    }, NORMALISE_COLOUR);

    assert(seen.title !== seen.background,
        `title ${seen.title} equals the container background ${seen.background} — this is ` +
        `exactly the 1.00:1 defect fixed in #8`);
    assert(seen.ticks !== seen.background, `tick labels ${seen.ticks} match the background`);
    return `title ${seen.title} on ${seen.background}`;
});

// ── 4. Phase bands and boundaries land on the right days (#9) ────────────────
await check('4  the x axis is linear and maps days, not indices', async () => {
    const geometry = await page.evaluate(() => {
        const chart = window.Chart.getChart('supercompChart');
        const x = chart.scales.x;
        const area = chart.chartArea;
        const at = (day) => (x.getPixelForValue(day) - area.left) / (area.right - area.left);
        return { type: x.type, atDay10: at(10), atDay20: at(20) };
    });

    assert(geometry.type === 'linear', `x scale is "${geometry.type}" — a category scale ` +
        `reads day values as point indices, which is the #9 defect`);
    // 3 sprints x 10 days: day 10 is a third of the way across, day 20 two thirds.
    assert(Math.abs(geometry.atDay10 - 1 / 3) < 0.02,
        `day 10 of 30 sits at ${(geometry.atDay10 * 100).toFixed(1)}% of the plot, expected 33.3%`);
    assert(Math.abs(geometry.atDay20 - 2 / 3) < 0.02,
        `day 20 of 30 sits at ${(geometry.atDay20 * 100).toFixed(1)}% of the plot, expected 66.7%`);
    return `day 10 at ${(geometry.atDay10 * 100).toFixed(1)}%, day 20 at ${(geometry.atDay20 * 100).toFixed(1)}%`;
});

// ── 5. Row identity survives a removal (#13) ─────────────────────────────────
await check('5  removing a member does not rebind other rows', async () => {
    await page.goto(BASE, { waitUntil: 'domcontentloaded' });
    await page.waitForSelector('.team-table tbody tr', { timeout: BOOT_TIMEOUT });

    // Stamp every name input with the value it currently shows. @key makes the DOM node
    // travel with its member, so after a removal each surviving node must still display
    // what it was stamped with. Without @key the nodes are matched by POSITION and the
    // node that held member 3 ends up showing member 4.
    const before = await page.evaluate(() => {
        const inputs = [...document.querySelectorAll('.team-table tbody tr input[type=text]')];
        inputs.forEach((el, i) => { el.dataset.stamped = el.value || `row${i}`; });
        return inputs.map((el) => el.value);
    });
    assert(before.length >= 4, `need several rows to test this, found ${before.length}`);

    await page.locator('.team-table tbody tr').nth(2).locator('.btn-remove').click();
    await page.waitForFunction(
        (n) => document.querySelectorAll('.team-table tbody tr').length === n - 1,
        before.length, { timeout: 15_000 });

    const drift = await page.evaluate(() => {
        const inputs = [...document.querySelectorAll('.team-table tbody tr input[type=text]')];
        return inputs
            .filter((el) => el.dataset.stamped !== undefined && el.dataset.stamped !== el.value)
            .map((el) => ({ stamped: el.dataset.stamped, now: el.value }));
    });

    assert(drift.length === 0,
        `${drift.length} surviving row(s) now show a different member than the DOM node ` +
        `they belong to: ${JSON.stringify(drift)}`);
    return `${before.length} rows -> ${before.length - 1}, every surviving node kept its member`;
});

// ── 6. Configuration survives a reload (#14) ─────────────────────────────────
await check('6  configuration survives a reload', async () => {
    const duration = page.locator('.param-card input[type=number]').first();
    await duration.fill('14');
    await duration.blur();
    // The debounce in AppStateService is 400ms; give it room without being generous
    // enough to hide a broken write.
    await page.waitForTimeout(1500);

    await page.reload({ waitUntil: 'domcontentloaded' });
    await page.waitForSelector('h2:has-text("Parametry Sprintu")', { timeout: BOOT_TIMEOUT });

    const restored = await page.locator('.param-card input[type=number]').first().inputValue();
    assert(restored === '14', `sprint duration came back as "${restored}" rather than "14"`);
    return 'sprint duration 14 restored from localStorage after a full reload';
});

// ── 7. Deep links and the SPA fallback (#15) ─────────────────────────────────
await check('7  deep links resolve through 404.html', async () => {
    const seen = [];
    for (const route of ['chart', 'data']) {
        const response = await page.goto(`${BASE}${route}`, { waitUntil: 'domcontentloaded' });
        // Pages serves 404.html with status 404; what matters is that the SPA boots and
        // routes rather than showing a static error page.
        // Wait for something only the BOOTED app produces. An earlier draft waited for
        // `.app-container`, which is in the static shell — so it passed against a stub
        // with no WebAssembly at all, i.e. it could not fail. The nav links are rendered
        // by MainLayout, so they exist only once Blazor is running.
        await page.waitForSelector('.header-nav .nav-link', { timeout: BOOT_TIMEOUT });
        const links = await page.locator('.header-nav .nav-link').count();
        assert(links === 3, `expected the three rendered nav links, found ${links}`);

        const routed = await page.evaluate(() => location.pathname);
        assert(routed.endsWith(`/${route}`), `expected to stay on /${route}, got ${routed}`);
        const body = await page.locator('.app-main').innerText();
        assert(body.trim().length > 0, `/${route} rendered an empty main region`);
        seen.push(`/${route} -> HTTP ${response.status()}, app booted and routed`);
    }
    return seen.join('; ');
});

// ── Report ───────────────────────────────────────────────────────────────────
await page.screenshot({ path: `${ARTIFACTS}/final.png`, fullPage: true });

const failed = results.filter((r) => !r.ok);
console.log('\n' + '-'.repeat(72));
console.log(`${results.length - failed.length}/${results.length} checks passed`);

if (consoleErrors.length) {
    console.log(`\nBrowser console errors (${consoleErrors.length}):`);
    for (const e of consoleErrors.slice(0, 10)) console.log(`  ${e}`);
}

await browser.close();

if (failed.length) {
    console.log(`\nFailed: ${failed.map((f) => f.name.trim().split(/\s{2,}/)[0]).join(', ')}`);
    process.exit(1);
}
