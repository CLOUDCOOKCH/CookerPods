# CookerPods connection lab

Read-only feasibility diagnostics for AirPods access on Windows. The original website tests browser APIs. After the browser connection failures, an optional native Windows preview now tests standard Windows device and advertisement APIs. Neither diagnostic is a working AirPods manager or a simulated device dashboard.

## Native Windows preview

[Download the Windows diagnostic preview](https://github.com/CLOUDCOOKCH/CookerPods/releases/tag/v0.4.1-native-preview).

See [download/run instructions and limitations](docs/native-windows.md). The self-contained x64/ARM64 preview needs no separately installed .NET runtime, driver replacement, or administrator request. It has no browser bridge or network API yet. It records paired-device status separately from unassociated nearby broadcasts, with deliberately experimental coarse battery estimates. Reports stay local and exclude names, addresses, IDs, and raw payloads. The Windows build workflow produces ZIPs and checksums and smoke-tests x64 startup; physical AirPods validation remains required.

## Browser-only website

Website visitors install no companion, extension, PWA, or driver. There is no application backend, external script, analytics, or automatic upload. The static page requests native browser permissions only after an explicit action. The native preview is a separate, explicitly installed experiment, not a hidden dependency of the website.

## Use on Windows

1. Publish the static page over HTTPS. The included workflow tests and deploys it to GitHub Pages when `main` is updated; see [hosting](docs/hosting.md) for setup and deployment verification.
2. Open the HTTPS page in current Edge on Windows 11. Pair your AirPods through ordinary Windows Settings, then select them using **Select device and check**. If they are not offered, record that outcome; don't change browser flags.
   If you have already selected them on this website, use **Find previously allowed devices**, then choose the device to reuse its browser grant without rediscovery. This lists only devices allowed for this origin/browser profile, not all Windows-connected devices. Reuse adds no service or broadcast permissions; use the chooser to request new permissions. Previously selected devices may be available even when no longer advertising, but actual GATT access can still fail.
3. Optionally request Apple broadcast-data access when selecting. **Check broadcasts** is available only if the selected-device API exists; observation ends automatically after 20 seconds. No nearby-device scan is started.
4. Download and inspect the report. Record the Edge version, Windows build, Intel adapter/driver, AirPods model/variant, and firmware separately where known. No device data is uploaded automatically.

The serial chooser check is optional and does not open or write to any port. No equivalent to ear-web's Nothing serial service has been established for AirPods. Cancel it if you do not recognize an entry.

## What the results mean

- Selection is not a successful protocol connection. GATT connection is not Windows audio status or AAP control access.
- Reports from probe 0.2.0 distinguish chooser failures (`selection: not-selected`), GATT failures (`gatt: not-connected`), and standard-service read failures. A chooser error does not establish that services were inspected. Repeated GATT failures may be a transport limitation; repeated unchanged retries are not a workaround.
- Only standard Battery and Device Information services are requested. Missing/denied services do **not** prove the absence of private services. Browser service grants limit discovery.
- A battery value is a one-time, unassigned standard value; no left/right/case mapping, continuous monitoring, or charging status is inferred.
- Raw advertisements, Bluetooth addresses, browser device IDs, selected names, and raw browser exception messages are omitted from reports. Standard model/firmware strings are included if read; review them before sharing.
- Broadcast API presence is not evidence of functional receipt. Zero events is inconclusive. This probe does not decrypt or decode Apple battery broadcasts.
- No settings commands, noise control, ear automation, or arbitrary Windows media control are implemented.

## Developer checks

Node 22 or newer runs `npm test` and `npm run check`. There are no npm dependencies or install step. For local development, run `python3 -m http.server 8000 --bind 127.0.0.1` from the repository. Loopback is for developers; visitors use HTTPS. Opening files directly can have different origin/permission behavior and is not the supported hardware-test baseline.

Tests use browser-API fakes to verify parsing, timeouts, cancellation, privacy, service failures, and watcher cleanup. They do not validate AirPods hardware. The target Windows/Edge/Intel/Pro 2 test remains outstanding.

On a developer machine with Playwright available, `npm run test:browser` runs integration checks against mocked hardware APIs and saves light/dark/mobile captures to `/tmp/cookerpods-review` by default. Set `COOKERPODS_CHROMIUM` and `COOKERPODS_SCREENSHOTS` for a different browser executable and output directory. These optional developer tools are provided by the current cloud environment and are not dependencies visitors install.

See [research and hardware procedure](docs/research.md). All source is original MIT-licensed code. Reference projects were used for research, not implementation copying. Not affiliated with Apple.
