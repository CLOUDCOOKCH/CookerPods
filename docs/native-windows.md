# Windows battery monitor preview 0.5.0

The native preview now opens a Windows app window with continuously observed, experimental battery estimates. It uses standard Windows Bluetooth drivers and ordinary user privileges. Renamed AirPods are not filtered out: the monitor does not query friendly names or require an AirPods name. No browser helper, server, driver, or settings/control transport is included.

## Download and run

1. [Download preview 0.5.0](https://github.com/CLOUDCOOKCH/CookerPods/releases/tag/v0.5.0-native-preview). Choose **win-arm64** for a Windows ARM PC, or **win-x64** for an Intel/AMD PC. Settings → System → About → System type identifies the architecture.
2. Extract the entire ZIP, then open **CookerPods.exe**. The runtime is bundled; no separate .NET installation is needed. Keep the license and notice files with it. This is an unsigned development preview; managed-PC policy may require IT approval.
3. Enable Windows Bluetooth and keep the existing AirPods pairing. Open the case nearby and take the earbuds out if no estimates appear. Compare candidate readings with a trusted battery reference, then explicitly choose a candidate. The app never selects automatically.
4. Read approximate **earbud slot A**, **earbud slot B**, and **case** values. Slot A/B are not verified physical left/right. Values use coarse ten-point steps. An unavailable case shows a dash rather than zero. **Pause** stops observation; **Resume** waits for a new packet from the selected candidate. Closing the window stops the watcher.
5. Use **Change** to choose again. A rotating address or a two-minute absence can expire a selection; the app does not silently follow another candidate. **Save report** writes a local JSON file only when you choose a path. Nothing uploads automatically.

**Nearby candidates are not authenticated as your AirPods or associated with your Windows audio connection.** Comparing readings is a useful manual choice, not proof of ownership. Other nearby devices may produce similar values. The window has no paired-device name filter, audio routing, ANC controls, ear automation, charging indicators, or encrypted-payload decryption.

## Freshness, privacy and accessibility

A selected estimate is live only when its decoded sample is at most 15 seconds old, the candidate has emitted a new packet during the current scan, and scanning is active. A newer unsupported frame makes the displayed older estimate historical. Stale/paused readings remain visible with their age and an explicit status; resuming alone never freshens them. Nearby entries expire after two minutes. The bounded 32-candidate collector evicts the least recently seen entry when full and never reuses its label during the session.

Addresses are used only in memory to group nearby frames, then discarded on expiry/close. The monitor stores no friendly names, Windows device identifiers or raw broadcasts. Reports include anonymous candidate labels, timestamps, decoder outcomes, experimental estimates, sanitized source issues and explicit limitations. Inspect a report before sharing. No listening port, service, automatic upload or persistence of the selected candidate is provided.

The resizable window follows Windows light/dark preference and high-contrast system colors, supports per-monitor DPI, keyboard controls, visible focus, accessible names and status announcements. A narrow window scrolls its content while keeping pause/report actions reachable. No formal accessibility conformance audit is claimed.

The executable requests ordinary-user execution (`asInvoker`). Deleting its extracted folder removes the application. A bundled single-file .NET executable may extract libraries to the normal per-user temporary .NET cache. No Bluetooth driver is installed.

## Optional diagnostic mode

The earlier bounded diagnostic remains available for troubleshooting:

```powershell
.\CookerPods.exe --diagnostic --seconds 30 --no-wait
.\CookerPods.exe --diagnostic --all-paired --no-wait
```

It starts passive observation immediately, collects adapter/paired metadata concurrently with a 20-second overall budget, and saves a schema 2 report under `%LOCALAPPDATA%\CookerPods\reports`. `--all-paired` permits names only in local console output, never in reports. A GUI-subsystem executable may not display console output when launched from Explorer; use the saved report or capture stdout/stderr from a developer subprocess. Diagnostic exit code zero means the report was saved, not that every Bluetooth check passed. Endpoint queries can overlap and cannot be summed as unique physical devices. The default live window does not run these metadata queries or create diagnostic report files.

## Hardware evidence and remaining limits

On the user's private Windows ARM64 PC (Windows 11 build 26200), preview 0.4.0 received an unassociated 100/100/80 estimate that roughly matched an independent reading. Preview 0.4.2 subsequently decoded 39 samples, with recent slot values 90%/100% and unavailable case; the user again confirmed a rough match and reported renamed AirPods. This supports trying a manually selected monitor. It does not establish authenticated identity, physical left/right mapping, reliable charging state, continuous reception during playback/sleep, or performance with multiple nearby devices.

The user has now executed the 0.5.0 window in the ARM64 testing context, independently confirmed fresh approximate 100%/100%/70% values, and supplied a fresh-reading export after a YouTube playback test. See [hardware observations and their limits](hardware-validation.md). Windows CI exercises the packaged x64 window and real WPF controls with explicitly synthetic frames, not real AirPods. Additional real tests should compare case open/closed, playback, pause/resume, sleep/resume and another nearby Apple device. No control features are enabled by these battery observations. [Noise-control research](noise-controls.md) identifies an additional kernel profile driver as the route used by the inspected Windows implementations; no verified route meeting the current standard-driver requirement was found.

## Developer build and validation

The SDK is pinned in `global.json`. `python3 scripts/setup-dotnet.py` installs it with official SHA-512 verification. Set `DOTNET_ROOT`, add it to PATH, and keep `DOTNET_CLI_HOME`/`NUGET_PACKAGES` writable. End users do not need developer tools.

```text
dotnet restore native/CookerPods.Diagnostic.Tests --locked-mode
dotnet test native/CookerPods.Diagnostic.Tests --no-restore
dotnet build native/CookerPods.Diagnostic.Windows -p:RestoreLockedMode=true
python3 scripts/package-native.py
```

Linux can build and cross-publish the WPF app but cannot execute it. Packages include .NET and Windows Desktop runtime licenses/notices, BUILDINFO provenance and SHA-256 checksums. An intentional runtime update must also update the upstream notices under `native/notices`. Uncommitted builds are labeled accordingly. Do not commit binaries, reports or device captures.

Windows CI runs native unit tests, publishes self-contained x64/ARM64 ZIPs, checks PE architectures, runs a bounded native diagnostic without assuming radio hardware, and exercises real WPF selection/pause/resume/change controls using synthetic fixtures. `--ui-smoke <directory>` is a developer-only explicit fixture mode; it never substitutes fake readings in the normal app. Client-area PNGs carry synthetic provenance, including light, dark, stale and narrow 200%-rendered captures. They do not establish actual monitor scaling at a physical 200% Windows desktop, screen-reader compatibility, or Bluetooth hardware behavior.

A matching `v<version>-native-preview` tag publishes tested packages after checksum/source-revision validation. `ui-review-<version>-<sha>` tags publish only synthetic review images/result metadata, with no app download, to make Windows UI evidence inspectable from this Linux environment.

## Protocol provenance

The documented layout was inspected on 14 October 2026 and rechecked on 15 October 2026 in [Continuity's proximity message description](https://github.com/furiousMAC/continuity/blob/d9fa98bddafbfa3baafc84e37a417d78a0598546/messages/proximity_pairing.md). It cites Celosia/Cunche's [Discontinued Privacy research](https://petsymposium.org/2020/files/papers/issue1/popets-2020-0003.pdf). This implementation copies no dissector code or captured fixtures. The format document is historical and is not a current-firmware guarantee. In particular, earbud role/mapping and nearby-device association remain unverified.

Microsoft’s [DeviceWatcher documentation](https://learn.microsoft.com/en-us/uwp/api/windows.devices.enumeration.devicewatcher) describes incremental events, initial enumeration completion, and nonblocking stop/unsubscription.

Windows APIs: [BluetoothLEAdvertisementWatcher](https://learn.microsoft.com/en-us/uwp/api/windows.devices.bluetooth.advertisement.bluetoothleadvertisementwatcher), [BluetoothDevice](https://learn.microsoft.com/en-us/uwp/api/windows.devices.bluetooth.bluetoothdevice), and [BluetoothAdapter](https://learn.microsoft.com/en-us/uwp/api/windows.devices.bluetooth.bluetoothadapter). [DeviceInformationKind](https://learn.microsoft.com/en-us/uwp/api/windows.devices.enumeration.deviceinformationkind) documents the distinction between the default device-interface view and explicit association endpoints and the Bluetooth protocol IDs. Official API metadata was read through Microsoft's documentation repository; actual unpackaged-app permissions and firmware behavior still need this hardware experiment.
