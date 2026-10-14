/* Original CookerPods code. MIT license. No reference-project implementation is included. */
(function (root) {
  'use strict';
  const SERVICES = Object.freeze(['battery_service', 'device_information']);
  const HELP = Object.freeze({
    NotFoundError: 'The requested item was not available. See the recorded operation; this does not establish the absence of private services.',
    NotAllowedError: 'Access was denied or the chooser was canceled. Check the site permission and try again.',
    SecurityError: 'Browser permission or policy blocked this operation. No conclusion about private services can be drawn.',
    NetworkError: 'The Bluetooth operation failed or the connection was lost. This may reflect a transport limitation, not just distance or pairing.',
    NotSupportedError: 'This browser or device does not support the requested operation.',
    TimeoutError: 'The operation exceeded its time limit. The requested result was not established; disconnect and try again.',
    InvalidStateError: 'The device or browser connection is not ready. Disconnect and try again.'
  });
  function explain(error, operation) {
    const name = typeof error?.name === 'string' && Object.hasOwn(HELP, error.name) ? error.name : 'OtherError';
    if (name === 'NotFoundError' && (operation === 'selection' || operation === 'serial')) {
      return { name, explanation: 'No device or port was selected: the chooser was canceled or no matching entry was available. GATT and device services were not tested by this attempt.' };
    }
    if (name === 'NetworkError' && operation === 'gatt') {
      return { name, explanation: 'The selected device could not open a browser GATT connection. Windows audio pairing does not establish GATT access. No services were read; repeated failure here may be a transport limitation.' };
    }
    if (name === 'NotFoundError' && ['battery', 'model', 'firmware'].includes(operation)) {
      return { name, explanation: 'The requested standard service or characteristic was not found. This is a service-read result, not chooser cancellation; private services remain untested.' };
    }
    return { name, explanation: HELP[name] || 'The operation failed. The report omits the raw error message to protect device information.' };
  }
  function batteryValue(view) {
    if (!view || view.byteLength !== 1) throw new Error('Invalid battery payload');
    const value = view.getUint8(0);
    if (value > 100) throw new Error('Invalid battery percentage');
    return value;
  }
  function textValue(view) {
    if (!view || view.byteLength < 1 || view.byteLength > 256) throw new Error('Invalid text payload');
    return new TextDecoder('utf-8', { fatal: true }).decode(view).replace(/[\u0000-\u001f\u007f-\u009f]/g, '').trim().slice(0, 128);
  }
  async function deadline(operation, milliseconds) {
    let timer;
    try {
      return await Promise.race([operation, new Promise((_, reject) => {
        timer = setTimeout(() => reject(Object.assign(new Error(), { name: 'TimeoutError' })), milliseconds);
      })]);
    } finally { clearTimeout(timer); }
  }
  class Probe extends EventTarget {
    constructor({ navigator, secure = true, timeoutMs = 20000, broadcastMs = 20000 } = {}) {
      super();
      this.nav = navigator || {};
      this.secure = secure;
      this.timeoutMs = timeoutMs;
      this.broadcastMs = broadcastMs;
      this.device = null;
      this.epoch = 0;
      this.busy = false;
      this.watcher = null;
      this.gattDisconnectListener = null;
      this.state = { status: 'Ready to check the standard battery and device-information services.', phase: 'idle', selected: false, connected: false, name: '', battery: null, model: null, firmware: null, observedAt: null, broadcastPermission: false, broadcasts: 0, broadcastStatus: 'Not checked', checks: [], capabilities: { secure, bluetooth: secure && typeof this.nav.bluetooth?.requestDevice === 'function', serial: secure && typeof this.nav.serial?.requestPort === 'function', broadcasts: false } };
    }
    update(values = {}) { Object.assign(this.state, values); this.dispatchEvent(new Event('change')); }
    record(check, outcome, detail, error = null) {
      this.state.checks.push({ time: new Date().toISOString(), check, outcome, detail, ...(error ? { error: explain(error, check) } : {}) });
      this.state.checks = this.state.checks.slice(-50);
      this.update();
    }
    async connect(includeBroadcasts = false) {
      if (this.busy) return;
      if (!this.state.capabilities.bluetooth) {
        this.record('browser', 'unavailable', 'Web Bluetooth requires a secure context and browser support.');
        this.update({ status: 'Web Bluetooth is unavailable in this context. Use an HTTPS page in current Edge.' });
        return;
      }
      this.disconnect(false);
      const epoch = ++this.epoch;
      this.busy = true;
      this.update({ phase: 'selecting', status: 'Select your AirPods in the browser chooser. Device selection is not proof of management support.' });
      let operation = 'selection';
      try {
        const options = { acceptAllDevices: true, optionalServices: [...SERVICES] };
        if (includeBroadcasts) options.optionalManufacturerData = [0x004c];
        const device = await this.nav.bluetooth.requestDevice(options);
        if (epoch !== this.epoch) return;
        operation = 'gatt';
        this.device = device;
        this.gattDisconnectListener = () => {
          if (this.device !== device) return;
          this.update({ connected: false, phase: 'disconnected', status: 'GATT disconnected. Any retained reading is a past observation, not current telemetry.' });
        };
        device.addEventListener('gattserverdisconnected', this.gattDisconnectListener);
        this.state.capabilities.broadcasts = typeof device.watchAdvertisements === 'function';
        this.update({ selected: true, name: device.name || 'Unnamed selected device', broadcastPermission: includeBroadcasts, phase: 'connecting', status: 'Device selected. Checking the GATT connection…' });
        this.record('selection', 'selected', 'A browser device was selected. Model and active Windows audio device are not verified.');
        if (!device.gatt) {
          this.record('gatt', 'unavailable', 'The selected browser device has no accessible GATT server.');
          this.update({ phase: 'complete', status: 'Selection worked, but no GATT server is accessible.' });
          return;
        }
        let timer;
        let expired = false;
        const attempt = device.gatt.connect();
        // A late connection must not remain open after timeout or cancellation.
        attempt.then(() => {
          const newerOwnsDevice = epoch !== this.epoch && this.device &&
            (this.device === device || (device.id && this.device.id === device.id));
          if ((expired || epoch !== this.epoch) && !newerOwnsDevice) device.gatt.disconnect();
        }, () => {}).catch(() => {});
        let server;
        try {
          server = await Promise.race([attempt, new Promise((_, reject) => {
            timer = setTimeout(() => { expired = true; reject(Object.assign(new Error(), { name: 'TimeoutError' })); }, this.timeoutMs);
          })]);
        } finally { clearTimeout(timer); }
        if (epoch !== this.epoch) return;
        this.update({ connected: true, phase: 'reading', status: 'GATT connected. Reading only the two granted standard services…' });
        this.record('gatt', 'connected', 'GATT connected. This does not establish AAP access or Windows audio status.');
        await this.read(server, 'battery_service', 'battery_level', 'battery', batteryValue, epoch);
        if (epoch !== this.epoch) return;
        await this.read(server, 'device_information', 'model_number_string', 'model', textValue, epoch);
        if (epoch !== this.epoch) return;
        await this.read(server, 'device_information', 'firmware_revision_string', 'firmware', textValue, epoch);
        if (epoch !== this.epoch) return;
        this.update({ phase: this.state.connected ? 'complete' : 'disconnected', status: this.state.battery !== null ? 'A standard battery value was read. Its component and reliability still need validation.' : 'Standard-service checks finished without a battery value. Private AirPods services remain untested.' });
      } catch (error) {
        if (epoch !== this.epoch) return;
        const detail = explain(error, operation).explanation;
        this.record(operation, operation === 'selection' ? 'not-selected' : 'not-connected', detail, error);
        this.update({ phase: 'complete', connected: false, status: detail });
      } finally {
        if (epoch === this.epoch) { this.busy = false; this.update(); }
      }
    }
    async read(server, serviceName, characteristicName, field, decode, epoch) {
      try {
        const value = await deadline((async () => {
          const service = await server.getPrimaryService(serviceName);
          if (epoch !== this.epoch) return null;
          const characteristic = await service.getCharacteristic(characteristicName);
          if (epoch !== this.epoch) return null;
          const data = await characteristic.readValue();
          return decode(data);
        })(), this.timeoutMs);
        if (epoch !== this.epoch) return;
        if (value === '') throw new Error('Empty text');
        this.update({ [field]: value, ...(field === 'battery' ? { observedAt: new Date().toISOString() } : {}) });
        this.record(field, 'read', `The standard ${field} characteristic returned a valid value.`);
      } catch (error) {
        if (epoch !== this.epoch) return;
        this.record(field, 'not-read', 'The requested standard characteristic could not be read. Private services are not assessed.', error);
      }
    }
    disconnect(record = true) {
      ++this.epoch;
      this.watcher?.abort();
      this.watcher = null;
      const device = this.device;
      if (device && this.gattDisconnectListener) device.removeEventListener('gattserverdisconnected', this.gattDisconnectListener);
      this.gattDisconnectListener = null;
      this.device = null;
      device?.gatt?.disconnect();
      this.busy = false;
      if (record) {
        this.record('session', 'stopped', 'This page stopped its connection/watch. Browser device grants are not revoked.');
        this.update({ connected: false, phase: 'disconnected', status: 'Session stopped. Retained readings are past observations; browser grants can be removed in site settings.' });
      } else {
        this.update({ selected: false, connected: false, name: '', battery: null, model: null, firmware: null, observedAt: null, broadcasts: 0, broadcastStatus: 'Not checked' });
      }
    }
    async broadcasts() {
      if (this.busy || !this.device || !this.state.broadcastPermission || !this.state.capabilities.broadcasts) return;
      const device = this.device;
      const epoch = this.epoch;
      const controller = new AbortController();
      this.watcher = controller;
      this.busy = true;
      let count = 0;
      let appleSections = 0;
      const received = (event) => {
        if (epoch !== this.epoch || controller.signal.aborted) return;
        count++;
        if (event.manufacturerData?.has(0x004c)) appleSections++;
        this.update({ broadcasts: count, broadcastStatus: `${count} event(s) received; ${appleSections} included permitted Apple manufacturer data. No payload decoding or battery attribution performed.` });
      };
      device.addEventListener('advertisementreceived', received);
      const timer = setTimeout(() => controller.abort(), this.broadcastMs);
      this.update({ broadcastStatus: 'Listening briefly for broadcasts from the selected browser device…' });
      try {
        const ended = new Promise(resolve => controller.signal.addEventListener('abort', resolve, { once: true }));
        await deadline(device.watchAdvertisements({ signal: controller.signal }), this.broadcastMs);
        await ended;
        if (epoch !== this.epoch) return;
        const detail = `${count} broadcast event(s); ${appleSections} with permitted Apple data. Zero events is inconclusive, not proof that the device never broadcasts.`;
        this.record('broadcasts', count ? 'observed' : 'inconclusive', detail);
        this.update({ broadcastStatus: detail });
      } catch (error) {
        if (epoch !== this.epoch) return;
        this.record('broadcasts', 'not-established', explain(error).explanation, error);
        this.update({ broadcastStatus: explain(error).explanation });
      } finally {
        clearTimeout(timer);
        controller.abort();
        device.removeEventListener('advertisementreceived', received);
        if (epoch === this.epoch) { this.watcher = null; this.busy = false; this.update(); }
      }
    }
    async serial() {
      if (this.busy || !this.state.capabilities.serial) return;
      this.busy = true;
      const epoch = this.epoch;
      this.update({ status: 'Inspecting the standard serial chooser. Select only a device you recognize. No port will be opened.' });
      try {
        const port = await this.nav.serial.requestPort();
        if (epoch !== this.epoch) return;
        const info = port.getInfo();
        const bluetooth = typeof info.bluetoothServiceClassId === 'string';
        this.record('serial', 'selected-only', bluetooth ? 'A Bluetooth serial port was selected. It was not opened, and AirPods identity/protocol are unverified.' : 'A port was selected without a reported Bluetooth service UUID. It was not opened.');
        this.update({ status: 'Chooser inspection finished. Selection alone does not prove AirPods communication.' });
      } catch (error) {
        if (epoch !== this.epoch) return;
        this.record('serial', 'not-selected', 'No port was selected or access failed. Unknown vendor services remain untested.', error);
        this.update({ status: 'Serial inspection did not establish a usable AirPods port.' });
      } finally { if (epoch === this.epoch) { this.busy = false; this.update(); } }
    }
    report() {
      return {
        schemaVersion: 1, probeVersion: '0.2.0', exportedAt: new Date().toISOString(),
        scope: 'Read-only browser feasibility. No AirPods control transport or exact model identity is established.',
        capabilities: { ...this.state.capabilities },
        session: { phase: this.state.phase, selected: this.state.selected, gattConnected: this.state.connected },
        observations: { standardBattery: this.state.battery, batteryObservedAt: this.state.observedAt, model: this.state.model, firmware: this.state.firmware, broadcastEvents: this.state.broadcasts },
        checks: this.state.checks.map(entry => ({ ...entry })),
        limitations: ['Only Battery and Device Information GATT services requested.', 'No raw AAP/L2CAP access or settings writes.', 'Missing data is not a zero battery or proof of no private services.', 'No serial port opened; no general nearby-device scan.', 'Device name, address, browser device ID, and raw advertisements excluded.']
      };
    }
  }
  const api = { Probe, batteryValue, textValue, explain, SERVICES };
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
  else root.CookerPodsProbe = api;
})(globalThis);
