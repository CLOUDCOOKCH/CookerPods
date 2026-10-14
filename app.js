'use strict';
const $ = id => document.getElementById(id);
const probe = new CookerPodsProbe.Probe({ navigator, secure: window.isSecureContext });
let renderedGrants = '';
function render() {
  const s = probe.state;
  $('secure-support').textContent = s.capabilities.secure ? 'Available' : 'HTTPS required';
  $('bluetooth-support').textContent = s.capabilities.bluetooth ? 'API present · device unverified' : 'Unavailable';
  $('serial-support').textContent = s.capabilities.serial ? 'API present · service unverified' : 'Unavailable';
  $('context-help').hidden = s.capabilities.secure && s.capabilities.bluetooth;
  $('connect').disabled = probe.busy || !s.capabilities.bluetooth;
  $('connect').textContent = probe.busy ? 'Check in progress…' : s.selected ? 'Select again and recheck' : 'Select device and check';
  $('disconnect').disabled = !probe.busy && !probe.device;
  $('broadcast-permission').disabled = probe.busy;
  $('serial').disabled = probe.busy || !s.capabilities.serial;
  $('find-granted').disabled = probe.busy || !s.capabilities.secure || typeof navigator.bluetooth?.getDevices !== 'function';
  $('granted-status').textContent = typeof navigator.bluetooth?.getDevices !== 'function'
    ? 'Listing previous website grants is unavailable in this browser. The site cannot list all Windows-paired devices.'
    : probe.grantedStatus;
  const grants = $('granted-devices');
  const signature = JSON.stringify(probe.grantedNames);
  if (renderedGrants !== signature) {
    renderedGrants = signature;
    grants.replaceChildren();
    probe.grantedNames.forEach((name, index) => {
      const li = document.createElement('li');
      const button = document.createElement('button');
      button.type = 'button'; button.className = 'secondary'; button.textContent = `Check ${name}`;
      button.addEventListener('click', () => probe.reconnectGranted(index));
      li.append(button); grants.append(li);
    });
  }
  for (const button of grants.querySelectorAll('button')) button.disabled = probe.busy;
  $('status').textContent = s.status;
  $('session-label').textContent = s.connected ? 'GATT connected' : s.selected ? 'Selected · no GATT connection' : 'No device selected';
  $('device-panel').hidden = !s.selected;
  $('device-name').textContent = s.name;
  $('battery').textContent = s.battery === null ? 'Unavailable / not read' : `${s.battery}% · single standard value`;
  $('model').textContent = s.model ?? 'Unavailable / not read';
  $('firmware').textContent = s.firmware ?? 'Unavailable / not read';
  $('reading-note').textContent = s.observedAt ? `One-time battery sample at ${new Date(s.observedAt).toLocaleTimeString()}. Not a live reading; its left/right/case association is unknown.` : 'No battery value is available. This does not prove the absence of private services.';
  $('broadcast').disabled = probe.busy || !probe.device || !s.broadcastPermission || !s.capabilities.broadcasts;
  $('broadcast-status').textContent = !s.capabilities.broadcasts ? 'Selected-device broadcast watching is unavailable or has not been established in this browser. Do not enable experimental flags.' : !s.broadcastPermission ? 'To test permitted Apple data, select again with the optional broadcast permission checked.' : s.broadcastStatus;
  const events = $('events');
  events.replaceChildren();
  if (!s.checks.length) { const li = document.createElement('li'); li.textContent = 'No checks performed yet.'; events.append(li); }
  for (const entry of s.checks) {
    const li = document.createElement('li');
    li.textContent = `${entry.check}: ${entry.outcome}. ${entry.detail}${entry.error ? ` (${entry.error.name})` : ''}`;
    events.append(li);
  }
}
probe.addEventListener('change', render);
$('connect').addEventListener('click', () => probe.connect($('broadcast-permission').checked));
$('disconnect').addEventListener('click', () => probe.disconnect());
$('broadcast').addEventListener('click', () => probe.broadcasts());
$('serial').addEventListener('click', () => probe.serial());
$('find-granted').addEventListener('click', () => probe.loadGrantedDevices());
$('export').addEventListener('click', () => {
  const blob = new Blob([JSON.stringify(probe.report(), null, 2)], { type: 'application/json' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url; a.download = 'cookerpods-browser-report.json'; a.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
});
window.addEventListener('pagehide', () => probe.disconnect(false));
render();
