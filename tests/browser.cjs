// Optional integration harness: requires Playwright and a Chromium executable.
const { chromium } = require('playwright');
const http = require('node:http');
const fs = require('node:fs/promises');
const path = require('node:path');
const assert = require('node:assert/strict');

async function main() {
  const root = path.resolve(__dirname, '..');
  const files = new Map([['/', ['index.html', 'text/html']], ['/styles.css', ['styles.css', 'text/css']], ['/probe.js', ['probe.js', 'text/javascript']], ['/app.js', ['app.js', 'text/javascript']]]);
  const server = http.createServer(async (req, res) => {
    const asset = files.get(req.url);
    if (!asset) { res.writeHead(404); res.end(); return; }
    try { res.writeHead(200, { 'Content-Type': asset[1] }); res.end(await fs.readFile(path.join(root, asset[0]))); }
    catch { res.writeHead(500); res.end(); }
  });
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  const address = `http://127.0.0.1:${server.address().port}`;
  let browser;
  try {
    browser = await chromium.launch({ executablePath: process.env.COOKERPODS_CHROMIUM || '/usr/bin/chromium', headless: true, args: ['--no-sandbox'] });
    const screenshots = process.env.COOKERPODS_SCREENSHOTS || '/tmp/cookerpods-review';
    await fs.mkdir(screenshots, { recursive: true });
    const context = await browser.newContext({ viewport: { width: 1440, height: 1100 }, acceptDownloads: true });
    await context.addInitScript(() => {
      window.mock = { mode: 'success', serialOpened: 0, requested: null, granted: [], chooserCalls: 0 };
      const error = name => Object.assign(new Error('private device address'), { name });
      const data = value => new DataView(Uint8Array.from(value).buffer);
      Object.defineProperty(navigator, 'bluetooth', { configurable: true, value: { requestDevice: async options => {
        mock.requested = options;
        mock.chooserCalls++;
        if (mock.mode === 'denied') throw error('NotAllowedError');
        const d = new EventTarget();
        d.name = '<img src=x onerror=alert(1)> private name';
        d.id = 'private-id';
        d.gatt = {
          connect: async () => ({ getPrimaryService: async service => {
            if (mock.mode === 'absent') throw error('NotFoundError');
            return { getCharacteristic: async characteristic => ({ readValue: async () => characteristic === 'battery_level' ? data([82]) : new DataView(new TextEncoder().encode(characteristic === 'model_number_string' ? 'Test model' : 'Test firmware').buffer) }) };
          } }),
          disconnect: () => d.dispatchEvent(new Event('gattserverdisconnected'))
        };
        d.watchAdvertisements = async ({ signal }) => {
          const e = new Event('advertisementreceived');
          e.manufacturerData = new Map([[0x004c, data([1, 2, 3])]]);
          if (!signal.aborted) d.dispatchEvent(e);
        };
        mock.granted = [d];
        return d;
      }, getDevices: async () => mock.granted } });
      Object.defineProperty(navigator, 'serial', { configurable: true, value: { requestPort: async () => ({ getInfo: () => ({ bluetoothServiceClassId: 'private-uuid' }), open: async () => { mock.serialOpened++; } }) } });
    });
    const page = await context.newPage();
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    await page.goto(address);
    await page.screenshot({ path: path.join(screenshots, 'desktop.png'), fullPage: true });
    assert.equal(await page.locator('#bluetooth-support').textContent(), 'API present · device unverified');
    await page.getByRole('button', { name: 'Find previously allowed devices' }).click();
    await page.waitForFunction(() => document.getElementById('granted-status').textContent.includes('No devices have been allowed'));
    assert.equal(await page.locator('#granted-devices button').count(), 0);
    await page.getByRole('button', { name: 'Select device and check' }).click();
    await page.waitForFunction(() => document.getElementById('battery').textContent.startsWith('82%'));
    assert.match(await page.locator('#device-name').textContent(), /<img/);
    assert.equal(await page.locator('#device-name img').count(), 0);
    assert.match(await page.locator('#reading-note').textContent(), /Not a live reading/);
    const downloadEvent = page.waitForEvent('download');
    await page.getByRole('button', { name: 'Download report' }).click();
    const download = await downloadEvent;
    const report = await fs.readFile(await download.path(), 'utf8');
    assert.equal(JSON.parse(report).observations.standardBattery, 82);
    assert.doesNotMatch(report, /private name|private-id|private device address/);
    await page.getByRole('button', { name: 'Disconnect / cancel' }).click();
    assert.match(await page.locator('#status').textContent(), /past observations/);
    await page.getByRole('button', { name: 'Find previously allowed devices' }).click();
    await page.locator('#granted-devices button').waitFor();
    assert.equal(await page.locator('#granted-devices img').count(), 0);
    const chooserCalls = await page.evaluate(() => mock.chooserCalls);
    await page.locator('#granted-devices button').click();
    await page.waitForFunction(() => document.getElementById('status').textContent.includes('component and reliability'));
    assert.equal(await page.evaluate(() => mock.chooserCalls), chooserCalls);
    assert.match(await page.locator('#events').textContent(), /previously allowed device was reused/);
    await page.evaluate(() => { mock.mode = 'absent'; });
    await page.getByRole('button', { name: 'Select again and recheck' }).click();
    await page.waitForFunction(() => document.getElementById('status').textContent.includes('Private AirPods services remain untested'));
    assert.equal(await page.locator('#battery').textContent(), 'Unavailable / not read');
    await page.evaluate(() => { mock.mode = 'denied'; });
    await page.getByRole('button', { name: 'Select again and recheck' }).click();
    await page.waitForFunction(() => document.getElementById('status').textContent.includes('Access was denied'));
    await page.getByText('Inspect the standard Bluetooth serial chooser', { exact: true }).click();
    await page.getByRole('button', { name: 'Inspect serial chooser', exact: true }).click();
    await page.waitForFunction(() => document.getElementById('status').textContent.includes('Chooser inspection finished'));
    assert.equal(await page.evaluate(() => mock.serialOpened), 0);
    await page.evaluate(() => { mock.mode = 'success'; probe.broadcastMs = 25; });
    await page.locator('#broadcast-permission').check();
    await page.getByRole('button', { name: 'Select device and check' }).click();
    await page.getByRole('button', { name: 'Check broadcasts for 20 seconds' }).waitFor();
    await page.getByRole('button', { name: 'Check broadcasts for 20 seconds' }).click();
    await page.waitForFunction(() => document.getElementById('broadcast-status').textContent.includes('1 broadcast event(s)'));
    assert.equal(await page.locator('#broadcast-status').getAttribute('role'), 'status');
    assert.deepEqual(await page.evaluate(() => mock.requested.optionalManufacturerData), [0x004c]);
    await page.setViewportSize({ width: 390, height: 844 });
    await page.screenshot({ path: path.join(screenshots, 'mobile.png'), fullPage: true });
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);
    await page.emulateMedia({ colorScheme: 'dark', reducedMotion: 'reduce' });
    await page.setViewportSize({ width: 1440, height: 1100 });
    await page.screenshot({ path: path.join(screenshots, 'desktop-dark.png'), fullPage: true });
    await page.evaluate(() => { Object.defineProperty(navigator, 'bluetooth', { value: undefined, configurable: true }); });
    // New document needs a separate context without the fake capability injection.
    const unsupported = await browser.newContext();
    await unsupported.addInitScript(() => {
      Object.defineProperty(navigator, 'bluetooth', { value: undefined });
      Object.defineProperty(navigator, 'serial', { value: undefined });
    });
    const noApi = await unsupported.newPage(); await noApi.goto(address);
    assert.equal(await noApi.locator('#connect').isDisabled(), true);
    assert.equal(await noApi.locator('#serial').isDisabled(), true);
    assert.equal(await noApi.locator('#find-granted').isDisabled(), true);
    assert.equal(await noApi.locator('#context-help').isVisible(), true);
    assert.deepEqual(errors, []);
    await unsupported.close(); await context.close();
    console.log('Browser integration passed: read, XSS-safe display, export privacy, disconnect, empty grants, reconnect without chooser, missing services, denied chooser, serial no-open, broadcast opt-in, missing APIs, mobile layout, dark/reduced-motion render. Hardware APIs were mocked.');
    console.log(`Screenshots: ${screenshots}`);
  } finally {
    await browser?.close();
    await new Promise(resolve => server.close(resolve));
  }
}
main().catch(error => { console.error(error); process.exitCode = 1; });
