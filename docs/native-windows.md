# Windows diagnostic preview 0.4.2

The native path is now explicitly allowed by the project owner after browser GATT connection failures. This diagnostic tests standard Windows access; it is not yet a browser companion, continuous battery monitor, or AirPods settings application. A native program alone does not establish a usable AAP/L2CAP control transport.

## Download and run

1. [Download preview 0.4.2](https://github.com/CLOUDCOOKCH/CookerPods/releases/tag/v0.4.2-native-preview). Choose `win-x64` for a normal Intel/AMD Windows 11 PC, or `win-arm64` for an ARM64 Windows PC. **Settings → System → About → System type** identifies the architecture; Bluetooth adapter brand alone does not establish CPU architecture.
2. Extract the entire ZIP to a folder you can write to, then open **CookerPods-Diagnostic.exe**. This is a self-contained, unsigned development preview: no separate .NET SDK/runtime installation is required. Keep its license/notice files with it.
3. Keep your existing Windows pairing and Bluetooth enabled. The program starts a 30-second broadcast observation immediately, while adapter and paired-device metadata are collected concurrently with an overall 20-second budget. Query messages can appear during the scan. For the first comparison, open the AirPods case nearby and take the earbuds out during the observation. Compare another run with the earbuds in the open case; record the physical state separately. Ctrl+C ends observation; it does not change pairing.
4. Read the local results and open the report path printed at the end. Reports are saved under `%LOCALAPPDATA%\CookerPods\reports`. Inspect the JSON before sharing it. Nothing is uploaded automatically.

The unsigned preview may be blocked by a managed PC’s policy. Follow your organization’s approval process; this release does not change that policy.

The app requests ordinary-user execution (`asInvoker`), opens no listening port, and installs no service or driver. Deleting the extracted folder removes the executable. Delete `%LOCALAPPDATA%\CookerPods` to remove saved reports. A bundled single-file .NET executable can extract runtime libraries to the normal per-user temporary .NET directory; those are runtime cache files, not a Bluetooth driver.

For renamed AirPods, open a terminal in the extracted folder and run:

```powershell
.\CookerPods-Diagnostic.exe --all-paired
```

This shows other paired names in the console only, not in the exported report. Anonymous entries are now exported regardless of whether a name contains AirPods; name matching is not a report filter. Additional options are `--seconds 10` through `--seconds 120`, `--no-wait`, and `--help`. Default invocation keeps the console open at the end until Enter is pressed. Exit code zero means the diagnostic finished and saved its report, not that Bluetooth or AirPods passed every check. Review `issues`, `adapter`, `scanStartedAtUtc`, `metadataCompletion`, `metadataSeconds`, `watcherStopReason`, and `observations`. Metadata deadlines leave a partial diagnostic rather than delaying the beginning of broadcast capture.

## What it checks

- Windows default Bluetooth adapter and its reported LE/central-role support.
- A Classic Bluetooth device-interface query plus explicit paired Classic/LE association-endpoint queries and their available OS connection properties. Friendly-name matching is a heuristic, not authenticated AirPods identification or audio-routing confirmation.
- Passive Windows BLE manufacturer-data reception, separate from any GATT connection and from the Windows audio connection.
- A deliberately limited experimental decoder for the documented Apple type `0x07`, 25-byte proximity-message layout. Other lengths, versions, and invalid battery values stay unknown. Coarse values use ten-point steps; 0xF is unavailable, not zero.

Nearby candidates have temporary labels. Their addresses are used only in memory to group events, then discarded. **Candidates are not linked to your paired AirPods.** Rotating addresses may create several candidates for one device, or nearby devices may belong to someone else. The console/report therefore shows earbud **slot A/B**, not a claimed physical left/right mapping. Charging flags, encrypted battery data, ear-state automation, precise model names, and controls are not implemented.

Names, addresses, Windows device IDs, raw payloads, and exception messages are omitted from reports. Standard error type/HRESULT, OS version, process architecture, non-unique model code, scan counts, approximate fields, and timestamps remain. Up to 32 proximity candidates are retained; newer unrecognized frames clear the latest estimate. Up to eight prior decoded samples per candidate remain in a separate timestamped history, explicitly historical and potentially stale. No unrelated Apple payload is retained. No-data results are inconclusive, not evidence of a zero battery or an unsupported entire platform.

## Reading a schema 2 report

`experimentalEstimate` describes only the candidate’s latest received frame and is `null` when that frame is unsupported. `latestDecodeOutcome` records a decode outcome or the first failing check. `decodeOutcomes` counts all retained-candidate frames by that outcome, including older callbacks. The 0.4.1 `UnsupportedByte10` gate incorrectly treated the historical example value of an undefined suffix as required. In 0.4.2 a nonzero byte 10 does not invalidate the earlier, documented cleartext battery fields: it yields `DecodedWithNonzeroSuffix` and `hasNonzeroSuffix: true` only if length, prefix and battery range checks pass. Confidence then explicitly notes an unverified firmware layout. This is not evidence of encryption or decryption. Bytes 11–26 are ignored. Reserved battery nibbles still remain unknown, never percentages above 100.

`lastDecodedEstimate` and `recentDecodedEstimates` preserve at most eight samples in event-time order, with `observedAtUtc` timestamps. They are not current readings. Compare their times with `finishedAtUtc`; a candidate’s newer `lastSeenUtc` must not make an older decode appear fresh. Fully unavailable battery fields remain unavailable even if the layout was decoded. This does not implement continuous monitoring or authenticated device association.

`pairedEnumeration` reports counts separately for `ClassicDeviceInterface`, `ClassicAssociationEndpoint`, and `LowEnergyAssociationEndpoint`. `pairedDevicesEnumerated` retains its legacy meaning: the raw count from the first, device-interface query only. At most 32 anonymous entries per query are reported, prioritizing name matches. Endpoint watchers retain at most 128 entries in memory and report truncation if that buffer or the output limit is exceeded. For watcher queries, `enumerated` counts the entries in the retained snapshot, not all devices that ever emitted an event. Sources can describe the same physical device, so do not sum them as unique devices. A failed query produces an issue rather than a fabricated zero count. Endpoint watchers observe for up to five seconds and report `completion` as `InitialEnumerationCompleted`, `PartialAtDeadline`, `Canceled`, or `StoppedBeforeInitialEnumeration`, with `observationSeconds`. Added/updated/removed events contribute to the bounded snapshot. An empty partial snapshot is inconclusive. Watchers are closed, stopped, and detached so late callbacks cannot change an exported snapshot. Metadata as a whole is limited to 20 seconds and its completion/time are reported separately. Classic FindAll results use `FindAllCompleted`. Classic connection-status reads are limited to four name matches; unmatched entries remain `NotRead`. Endpoint queries use `System.Devices.Aep.IsConnected` when available. Every status records its source and does not establish active audio routing. `--all-paired` displays other names only in the local console.

## Hardware acceptance still required

The 0.4.0 preview executed on a user’s ARM64 Windows PC and received supported and unsupported proximity broadcasts. One unassociated 100/100/80 estimate roughly matched the user’s independent battery reference. This is limited feasibility evidence; it does not validate physical identity, repeatability, left/right mapping, charging behavior, or this updated release. No real AirPods or Windows radio is attached to the cloud build machine. A compiled preview and synthetic tests do not establish usable telemetry. Run on Windows 11 25H2 with the actual Bluetooth adapter and AirPods Pro 2. Record adapter/driver, AirPods variant/firmware, case/earbud positions, playback state, and a near-time trusted battery reference separately. Compare case open/closed, charging, playback, sleep/resume, and a second nearby Apple device before enabling any real monitor feature. Do not use administrator privileges to mask an access-denied result; report its stage/code.

This preview is a native transport experiment. The next implementation gate is useful, correctly associated, repeatable real telemetry. Only then should a restricted, authenticated browser-to-companion interface be implemented. No API exposing arbitrary Bluetooth operations is provided.

## Developer build

The SDK is pinned in `global.json`. Linux cloud setup: `python3 scripts/setup-dotnet.py` downloads that SDK from Microsoft's official release metadata and verifies SHA-512 before extraction. Set `DOTNET_ROOT`, add it to `PATH`, and keep `DOTNET_CLI_HOME`/`NUGET_PACKAGES` in writable cache locations. End users do not need these tools.

```text
dotnet restore native/CookerPods.Diagnostic.Tests --locked-mode
dotnet test native/CookerPods.Diagnostic.Tests --no-restore
dotnet build native/CookerPods.Diagnostic.Windows -p:RestoreLockedMode=true
python3 scripts/package-native.py
```

Native projects share the supported Windows runtime-identifier list so self-contained publish and ordinary test/build restore graphs remain consistent. The package script uses locked restore, emits self-contained x64/ARM64 ZIPs with runtime notices and SHA-256 checksums, and does not mark local uncommitted builds as a committed source revision. `artifacts/native` contains disposable outputs. Do not commit binaries, reports, or captured device data.

A matching `v<version>-native-preview` tag or explicit manual publish runs the gated release job. It verifies the downloaded build’s checksums, version, and source revision before publication; it does not replace an existing release.

Windows CI launches the packaged x64 executable with `--help`, then runs `scripts/smoke-native-windows.py`. The latter executes a ten-second diagnostic with a 60-second process deadline, validates that a new versioned report was written, and checks metadata bounds and early capture timing when the watcher starts. Bluetooth errors or absence are recorded in the report; this is an API/fallback smoke test without assumed radio hardware, not a hardware validation. ARM64 is cross-published and its PE architecture checked; it requires a real ARM64 run for execution validation. No code signing or physical-hardware validation is claimed.

## Protocol provenance

The documented layout was inspected on 14 October 2026 and rechecked on 15 October 2026 in [Continuity's proximity message description](https://github.com/furiousMAC/continuity/blob/d9fa98bddafbfa3baafc84e37a417d78a0598546/messages/proximity_pairing.md). It cites Celosia/Cunche's [Discontinued Privacy research](https://petsymposium.org/2020/files/papers/issue1/popets-2020-0003.pdf). This implementation copies no dissector code or captured fixtures. The format document is historical and is not a current-firmware guarantee. In particular, earbud role/mapping and nearby-device association remain unverified.

Microsoft’s [DeviceWatcher documentation](https://learn.microsoft.com/en-us/uwp/api/windows.devices.enumeration.devicewatcher) describes incremental events, initial enumeration completion, and nonblocking stop/unsubscription.

Windows APIs: [BluetoothLEAdvertisementWatcher](https://learn.microsoft.com/en-us/uwp/api/windows.devices.bluetooth.advertisement.bluetoothleadvertisementwatcher), [BluetoothDevice](https://learn.microsoft.com/en-us/uwp/api/windows.devices.bluetooth.bluetoothdevice), and [BluetoothAdapter](https://learn.microsoft.com/en-us/uwp/api/windows.devices.bluetooth.bluetoothadapter). [DeviceInformationKind](https://learn.microsoft.com/en-us/uwp/api/windows.devices.enumeration.deviceinformationkind) documents the distinction between the default device-interface view and explicit association endpoints and the Bluetooth protocol IDs. Official API metadata was read through Microsoft's documentation repository; actual unpackaged-app permissions and firmware behavior still need this hardware experiment.
