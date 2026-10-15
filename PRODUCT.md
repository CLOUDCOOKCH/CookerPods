# Product

<!-- impeccable:product-schema 1 -->

## Platform

web diagnostic; Windows desktop monitor

## Users

The user wants AirPods management on Windows, originally browser-only with ambitions comparable to ear-web. Native installation and a Windows app window were explicitly authorized after browser access failed. The browser evaluation target is AirPods Pro 2 on Windows 11 25H2/current Edge with an existing Intel Bluetooth driver. Native previews have since been tested by the user on a private Windows ARM64 PC with renamed AirPods Pro 2.

## Product Purpose

The product goal is full AirPods management in Windows. The public 0.5.0 preview is an experimental battery monitor; browser GATT failures led the user to authorize native installation and a Windows window. After the user clarified the full-management goal, 0.6 development adds an original ARM64/x64 profile-driver transport and a report-driven control UI. Production driver signing and real control hardware validation are outstanding; full management is not yet shipped.

## Operating Context

Visitors open an HTTPS static page and explicitly select a device in a browser chooser or reuse a device previously allowed for this website in the current browser profile. Windows audio pairing and website access are separate: connected AirPods may be absent from the chooser, and Windows pairing alone does not grant website access. Results remain in the tab until refresh or closure. Downloaded JSON reports are local and can be inspected before sharing. A GitHub Pages workflow tests and deploys updates to `main`; successful publication still requires a completed deployment and site verification.

## Capabilities and Constraints

- The website remains installation-free. The user has now explicitly allowed a separate native Windows preview after browser-only access failed. The released battery monitor uses standard Windows drivers and ordinary privileges. Full-management development now uses an additional kernel profile driver for AAP controls; no driver installation or security-policy change has been performed. A signed driver package is a separate distribution prerequisite. The website still requires no extension, PWA or browser flags.
- The native monitor uses explicit anonymous nearby-candidate selection independent of friendly names, including renamed AirPods. It marks estimates stale after 15 seconds, expires candidates after two minutes, and stops scanning on pause/close. Case unavailability remains unknown. The released monitor has no browser bridge, listening endpoint, settings writes or cloud backend. Development controls use an explicit paired-device driver session and do not reuse BLE candidate identity. Paired-device status and nearby broadcast candidates are separate. Experimental battery estimates are unassociated and do not establish physical left/right mapping. See `docs/native-windows.md` for the hardware gate.
- The diagnostic requests standard Battery and Device Information services and may read one unassigned battery sample, model, and firmware. It sends no settings commands. Selection and GATT connection do not establish AirPods identity, active Windows audio status, or management support.
- **Find previously allowed devices** uses `navigator.bluetooth.getDevices()` where available to list this origin's existing grants in the current browser profile. A per-device **Check** reuses that grant without reopening the chooser or adding service/manufacturer permissions. This does not list Windows-paired devices, guarantee a GATT connection, or establish AirPods support. First-time website access still requires the browser chooser.
- Optional Apple manufacturer-data permission permits a 20-second observation of the selected device where supported. No general scan, raw-data export, decryption, or battery decoding is implemented. API presence and zero received events are inconclusive.
- The optional Web Serial chooser only inspects a selected port; it never opens or writes to it. An AirPods equivalent to ear-web's Nothing serial transport has not been established.
- Reports exclude device names, addresses, browser IDs, raw broadcasts, and raw exception messages. Model and firmware strings may be included. There is no backend, analytics, or automatic upload.
- The experimental monitor permits a manual comparison-based candidate choice; authenticated device association and continuous real-hardware reliability remain unverified. Controls require a separately demonstrated command transport and physical readback. See `docs/research.md` for the hardware gate.

## Brand Commitments

CookerPods is independent, open-source research, unaffiliated with Apple. The user pinned restrained Apple-inspired materials, system fonts, light/dark appearance, and accessibility. Original imagery or other assets are unnecessary for this diagnostic.

## Evidence on Hand

`index.html`, `styles.css`, `app.js`, `probe.js`, `README.md`, and `docs/` describe the implemented diagnostic. Unit and browser integration checks use mocked browser hardware APIs; they do not validate AirPods support. Review captures in `/tmp/cookerpods-review/` show desktop initial, mobile connected, and desktop dark connected states with mocked devices, including an intentionally malicious name rendered as plain text. These captures are temporary development evidence, not product proof. Browser hardware tests failed to establish GATT access. Native 0.4.0 and 0.4.2 ARM64 reports yielded approximate readings the user independently confirmed. The user subsequently ran the 0.5.0 live window: fresh 100%/100%/70% approximate readings matched an independent reference, and a YouTube playback test still exported fresh earbud values with unavailable case. These snapshots do not establish uninterrupted sessions or ownership. See docs/hardware-validation.md. Noise-control research found driver-dependent Windows implementations but no verified standard-driver route for this ARM64 target; see docs/noise-controls.md. The user clarified that full Windows management is the priority, and the original driver-backed development is documented in docs/control-development.md. Synthetic WPF captures validate layout/controls only.

## Product Principles

- Establish a working browser transport before promising features.
- Keep the installation-free browser lab separate from the explicitly authorized native window.
- Distinguish observations, permissions, device identity, and actual capabilities.
- Keep device access explicit and reports local, minimal, and inspectable.

## Accessibility & Inclusion

Preserve keyboard access, visible focus, native controls, readable contrast in both system color schemes, reduced-motion support, responsive reflow, and announced status updates. No formal accessibility conformance audit is claimed.
