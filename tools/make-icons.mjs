// Renders tools/icon.html into the PNG icons used by the web app and the native apps.
// Usage: node tools/make-icons.mjs   (needs Playwright with a Chromium browser)
import { chromium } from 'playwright';
import { mkdirSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const page = 'file://' + path.join(root, 'tools/icon.html');
const jobs = [
  // web app
  { out: 'icons/icon-192.png', size: 192 },
  { out: 'icons/icon-512.png', size: 512 },
  { out: 'icons/icon-maskable-512.png', size: 512, pad: 0.1 },
  { out: 'icons/apple-touch-icon.png', size: 180 },
  { out: 'icons/favicon-32.png', size: 32 },
  // sources for @capacitor/assets (native icons and splash screens)
  { out: 'assets/icon-only.png', size: 1024 },
  { out: 'assets/icon-foreground.png', size: 1024, pad: 0.18 },
  { out: 'assets/icon-background.png', size: 1024, background: true },
  { out: 'assets/splash.png', splash: true, w: 2732, h: 2732, pad: 0.3 },
  { out: 'assets/splash-dark.png', splash: true, w: 2732, h: 2732, pad: 0.3 },
];

mkdirSync(path.join(root, 'icons'), { recursive: true });
mkdirSync(path.join(root, 'assets'), { recursive: true });
const browser = await chromium.launch();
for (const j of jobs) {
  const w = j.w || j.size, h = j.h || j.size;
  const p = await browser.newPage({ viewport: { width: w, height: h } });
  const qs = new URLSearchParams({ size: String(j.size || w), pad: String(j.pad || 0) });
  if (j.splash) { qs.set('splash', '1'); qs.set('w', String(w)); qs.set('h', String(h)); }
  await p.goto(`${page}?${qs}`);
  await p.waitForFunction(() => document.title === 'ready');
  if (j.background) {
    // plain sky for Android's adaptive-icon background layer
    await p.evaluate(() => {
      const c = document.getElementById('c'), x = c.getContext('2d');
      const g = x.createLinearGradient(0, 0, 0, c.height); g.addColorStop(0, '#8fcbe6'); g.addColorStop(1, '#e3f2f8');
      x.fillStyle = g; x.fillRect(0, 0, c.width, c.height);
    });
  }
  await p.locator('#c').screenshot({ path: path.join(root, j.out), omitBackground: true });
  await p.close();
  console.log('wrote', j.out);
}
await browser.close();
