const { test } = require('node:test');
const assert = require('node:assert/strict');
const { Probe, batteryValue, textValue } = require('../probe.js');
const namedError = name => Object.assign(new Error('private device AA:BB:CC:DD:EE:FF'), { name });
const bytes = values => new DataView(Uint8Array.from(values).buffer);
const text = value => new DataView(new TextEncoder().encode(value).buffer);
function device({ battery = 74, absent = [], failConnect = false } = {}) {
  const d = new EventTarget();
  d.name = '<private device name>';
  d.id = 'private-browser-id';
  d.reads = [];
  d.gatt = {
    connected: false,
    connect: async () => {
      if (failConnect) throw namedError('NetworkError');
      d.gatt.connected = true;
      return {
        getPrimaryService: async service => {
          if (absent.includes(service)) throw namedError('NotFoundError');
          return { getCharacteristic: async characteristic => ({ readValue: async () => {
            d.reads.push(characteristic);
            if (characteristic === 'battery_level') return bytes([battery]);
            return text(characteristic === 'model_number_string' ? 'Model-unverified' : 'Firmware-unverified');
          } }) };
        }
      };
    },
    disconnect: () => { d.gatt.connected = false; d.dispatchEvent(new Event('gattserverdisconnected')); }
  };
  return d;
}
function probeFor(d, config = {}) {
  return new Probe({ navigator: { bluetooth: { requestDevice: async options => { d.options = options; return d; } } }, timeoutMs: 20, broadcastMs: 20, ...config });
}
test('reads only granted standard fields, requests no broadcast access by default', async () => {
  const d = device(); const p = probeFor(d);
  await p.connect();
  assert.equal(p.state.battery, 74);
  assert.equal(p.state.connected, true);
  assert.deepEqual(d.options, { acceptAllDevices: true, optionalServices: ['battery_service', 'device_information'] });
  assert.deepEqual(d.reads, ['battery_level', 'model_number_string', 'firmware_revision_string']);
  assert.match(p.state.status, /component and reliability/);
  p.disconnect();
  assert.equal(d.gatt.connected, false);
  assert.equal(p.state.battery, 74); // Historical, explicitly labeled in the UI.
});
test('absent standard services stay unknown, not zero or universally unsupported', async () => {
  const p = probeFor(device({ absent: ['battery_service', 'device_information'] }));
  await p.connect();
  assert.equal(p.state.battery, null);
  assert.equal(p.state.connected, true);
  assert.equal(p.state.checks.filter(c => c.outcome === 'not-read').length, 3);
  assert.match(p.state.status, /Private AirPods services remain untested/);
  p.disconnect();
});
test('invalid or oversized payloads cannot become plausible readings', async () => {
  for (const values of [[], [101], [255], [55, 22]]) assert.throws(() => batteryValue(bytes(values)));
  assert.equal(batteryValue(bytes([0])), 0);
  assert.throws(() => textValue(bytes(Array(257).fill(65))));
  assert.throws(() => textValue(bytes([255])));
  const p = probeFor(device({ battery: 255 })); await p.connect();
  assert.equal(p.state.battery, null);
  assert.equal(p.state.model, 'Model-unverified');
  p.disconnect();
});
test('denied chooser reports privacy-safe error and can be retried', async () => {
  const p = new Probe({ navigator: { bluetooth: { requestDevice: async () => { throw namedError('NotAllowedError'); } } } });
  await p.connect();
  assert.equal(p.state.selected, false);
  assert.equal(p.busy, false);
  assert.equal(p.state.checks.at(-1).error.name, 'NotAllowedError');
  assert.doesNotMatch(JSON.stringify(p.report()), /private device|AA:BB/);
});
test('insecure origin never invokes a chooser', async () => {
  let invoked = false;
  const p = new Probe({ secure: false, navigator: { bluetooth: { requestDevice: async () => { invoked = true; } } } });
  await p.connect(); assert.equal(invoked, false);
  assert.equal(p.state.capabilities.bluetooth, false);
});
test('chooser cancellation does not imply standard services were checked', async () => {
  const p = new Probe({ navigator: { bluetooth: { requestDevice: async () => { throw namedError('NotFoundError'); } } } });
  await p.connect();
  const result = p.state.checks.at(-1);
  assert.equal(result.check, 'selection');
  assert.equal(result.outcome, 'not-selected');
  assert.match(result.error.explanation, /services were not tested/);
  assert.doesNotMatch(result.error.explanation, /characteristic was not found/);
});
test('GATT failure is recorded before service reads and does not promise a retry fix', async () => {
  const d = device({ failConnect: true });
  const p = probeFor(d); await p.connect();
  const result = p.state.checks.at(-1);
  assert.equal(p.state.selected, true);
  assert.equal(result.check, 'gatt');
  assert.equal(result.outcome, 'not-connected');
  assert.equal(result.error.name, 'NetworkError');
  assert.match(result.error.explanation, /No services were read/);
  assert.match(result.error.explanation, /transport limitation/);
  assert.deepEqual(d.reads, []);
  p.disconnect();
});
test('missing services are separate from chooser cancellation in exported errors', async () => {
  const p = probeFor(device({ absent: ['battery_service'] }));
  await p.connect();
  const result = p.report().checks.find(c => c.check === 'battery');
  assert.equal(result.outcome, 'not-read');
  assert.match(result.error.explanation, /service-read result/);
  assert.equal(p.report().probeVersion, '0.2.0');
  p.disconnect();
});
test('canceled pending selection cannot overwrite a newer session', async () => {
  let resolve;
  const d = device();
  const p = new Probe({ navigator: { bluetooth: { requestDevice: () => new Promise(r => { resolve = r; }) } } });
  const run = p.connect(); p.disconnect(); resolve(d); await run;
  assert.equal(p.state.selected, false);
  assert.equal(p.state.connected, false);
  assert.equal(d.gatt.connected, false);
});
test('late GATT connection is closed after timeout', async () => {
  const d = device(); let resolve;
  d.gatt.connect = () => new Promise(r => { resolve = r; });
  const p = probeFor(d, { timeoutMs: 5 });
  await p.connect();
  assert.equal(p.state.connected, false);
  assert.equal(p.state.checks.at(-1).error.name, 'TimeoutError');
  d.gatt.connected = true; resolve({}); await new Promise(r => setTimeout(r, 0));
  assert.equal(d.gatt.connected, false);
});
test('stalled service reads time out without hanging the session', async () => {
  const d = device();
  d.gatt.connect = async () => ({ getPrimaryService: () => new Promise(() => {}) });
  const p = probeFor(d, { timeoutMs: 5 });
  await p.connect();
  assert.equal(p.busy, false);
  assert.equal(p.state.battery, null);
  assert.equal(p.state.checks.filter(c => c.error?.name === 'TimeoutError').length, 3);
  p.disconnect();
});
for (const reuse of [true, false]) {
  test(`late canceled connect cannot disconnect a newer session (${reuse ? 'same object' : 'same device ID'})`, async () => {
    const firstDevice = device();
    const secondDevice = reuse ? firstDevice : device();
    let resolveFirst;
    let selections = 0;
    let connections = 0;
    let disconnects = 0;
    const realConnect = firstDevice.gatt.connect;
    const realDisconnect = firstDevice.gatt.disconnect;
    firstDevice.gatt.connect = () => ++connections === 1 ? new Promise(resolve => { resolveFirst = resolve; }) : realConnect();
    firstDevice.gatt.disconnect = () => { disconnects++; realDisconnect(); };
    const p = new Probe({ timeoutMs: 1000, navigator: { bluetooth: { requestDevice: async () => ++selections === 1 ? firstDevice : secondDevice } } });
    const first = p.connect();
    await new Promise(resolve => setImmediate(resolve));
    p.disconnect();
    await p.connect();
    assert.equal(p.state.connected, true);
    const before = disconnects;
    resolveFirst({}); await first;
    assert.equal(disconnects, before);
    assert.equal(p.state.connected, true);
    assert.equal(p.state.phase, 'complete');
    p.disconnect();
  });
}
test('serial chooser is selection-only and never opens a port', async () => {
  let opens = 0;
  const p = new Probe({ navigator: { serial: { requestPort: async () => ({
    getInfo: () => ({ bluetoothServiceClassId: 'private-service', usbVendorId: 1 }),
    open: () => { opens++; }
  }) } } });
  await p.serial();
  assert.equal(opens, 0);
  assert.equal(p.state.checks.at(-1).outcome, 'selected-only');
  assert.doesNotMatch(JSON.stringify(p.report()), /private-service|usbVendorId/);
});
test('report excludes names, browser identifiers, and raw error messages', async () => {
  const p = probeFor(device()); await p.connect();
  const report = JSON.stringify(p.report());
  assert.doesNotMatch(report, /private device|private-browser-id|AA:BB/);
  assert.match(report, /single|standardBattery/);
  p.disconnect();
});
test('broadcast watching requires explicit permission and exports counts only', async () => {
  const d = device(); let watching = 0;
  d.watchAdvertisements = async ({ signal }) => {
    watching++;
    const event = new Event('advertisementreceived');
    event.manufacturerData = new Map([[0x004c, bytes([9, 8, 7])]]);
    d.dispatchEvent(event);
    signal.addEventListener('abort', () => { watching--; }, { once: true });
  };
  const p = probeFor(d, { broadcastMs: 5 });
  await p.connect(); await p.broadcasts();
  assert.equal(watching, 0); assert.equal(p.state.broadcasts, 0);
  await p.connect(true);
  assert.deepEqual(d.options.optionalManufacturerData, [0x004c]);
  await p.broadcasts();
  assert.equal(watching, 0); assert.equal(p.state.broadcasts, 1);
  assert.equal(p.state.checks.at(-1).outcome, 'observed');
  assert.doesNotMatch(JSON.stringify(p.report()), /manufacturerData|9,8,7/);
  p.disconnect();
});
test('zero broadcast events remain inconclusive and the watcher is stopped', async () => {
  const d = device();
  d.watchAdvertisements = async () => {};
  const p = probeFor(d, { broadcastMs: 5 });
  await p.connect(true); await p.broadcasts();
  assert.equal(p.busy, false);
  assert.equal(p.state.checks.at(-1).outcome, 'inconclusive');
  assert.equal(p.watcher, null);
  p.disconnect();
});
