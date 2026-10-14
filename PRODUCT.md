# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Users

The intended user wants to manage AirPods directly in a browser, with an experience comparable in ambition to ear-web. The current evaluation target is AirPods Pro 2 on Windows 11 25H2, current Microsoft Edge, and an existing Intel Bluetooth adapter/driver.

## Product Purpose

CookerPods currently provides a read-only transport feasibility diagnostic. Its immediate purpose is to establish what the browser can actually access before building a manager. A usable browser-only AirPods manager remains the future goal; the current page does not fulfill that goal or establish hardware support.

## Operating Context

Visitors open an HTTPS static page, pair their AirPods in ordinary Windows Settings, and explicitly select a device in a browser chooser. Results remain in the tab until refresh or closure. Downloaded JSON reports are local and can be inspected before sharing. A GitHub Pages workflow tests and deploys updates to `main`; successful publication still requires a completed deployment and site verification.

## Capabilities and Constraints

- No companion application, extension, custom driver, PWA installation, or experimental browser flags. These are binding constraints, including if browser transport proves insufficient.
- The diagnostic requests standard Battery and Device Information services and may read one unassigned battery sample, model, and firmware. It sends no settings commands. Selection and GATT connection do not establish AirPods identity, active Windows audio status, or management support.
- Optional Apple manufacturer-data permission permits a 20-second observation of the selected device where supported. No general scan, raw-data export, decryption, or battery decoding is implemented. API presence and zero received events are inconclusive.
- The optional Web Serial chooser only inspects a selected port; it never opens or writes to it. An AirPods equivalent to ear-web's Nothing serial transport has not been established.
- Reports exclude device names, addresses, browser IDs, raw broadcasts, and raw exception messages. Model and firmware strings may be included. There is no backend, analytics, or automatic upload.
- Live monitoring requires useful, correctly associated, repeatable telemetry on real hardware. Controls require a separately demonstrated command transport and physical readback. See `docs/research.md` for the hardware gate.

## Brand Commitments

CookerPods is independent, open-source research, unaffiliated with Apple. The user pinned restrained Apple-inspired materials, system fonts, light/dark appearance, and accessibility. Original imagery or other assets are unnecessary for this diagnostic.

## Evidence on Hand

`index.html`, `styles.css`, `app.js`, `probe.js`, `README.md`, and `docs/` describe the implemented diagnostic. Unit and browser integration checks use mocked browser hardware APIs; they do not validate AirPods support. Review captures in `/tmp/cookerpods-review/` show desktop initial, mobile connected, and desktop dark connected states with mocked devices, including an intentionally malicious name rendered as plain text. These captures are temporary development evidence, not product proof. Real Windows/Edge/Intel/AirPods Pro 2 hardware testing remains outstanding.

## Product Principles

- Establish a working browser transport before promising features.
- Preserve the browser-only constraint through research and implementation.
- Distinguish observations, permissions, device identity, and actual capabilities.
- Keep device access explicit and reports local, minimal, and inspectable.

## Accessibility & Inclusion

Preserve keyboard access, visible focus, native controls, readable contrast in both system color schemes, reduced-motion support, responsive reflow, and announced status updates. No formal accessibility conformance audit is claimed.
