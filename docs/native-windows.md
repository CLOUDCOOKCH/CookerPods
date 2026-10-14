# Windows diagnostic preview 0.4.0

The native path is now explicitly allowed by the project owner after browser GATT connection failures. This diagnostic tests standard Windows access; it is not yet a browser companion, continuous battery monitor, or AirPods settings application. A native program alone does not establish a usable AAP/L2CAP control transport.

## Download and run

1. Choose `win-x64` for a normal Intel/AMD Windows 11 PC, or `win-arm64` for an ARM64 Windows PC. **Settings → System → About → System type** identifies the architecture; Bluetooth adapter brand alone does not establish CPU architecture.
2. Extract the entire ZIP to a folder you can write to, then open **CookerPods-Diagnostic.exe**. This is a self-contained, unsigned development preview: no separate .NET SDK/runtime installation is required. Keep its license/notice files with it.
3. Keep your existing Windows pairing and Bluetooth enabled. The program shows matching paired-device names locally, then observes broadcasts for 30 seconds. Open the AirPods case nearby during the observation. Ctrl+C ends observation; it does not change pairing.
4. Read the local results and open the report path printed at the end. Reports are saved under `%LOCALAPPDATA%\CookerPods\reports`. Inspect the JSON before sharing it. Nothing is uploaded automatically.

The app requests ordinary-user execution (`asInvoker`), opens no listening port, and installs no service or driver. Deleting the extracted folder removes the executable. Delete `%LOCALAPPDATA%\CookerPods` to remove saved reports. A bundled single-file .NET executable can extract runtime libraries to the normal per-user temporary .NET directory; those are runtime cache files, not a Bluetooth driver.

For renamed AirPods, open a terminal in the extracted folder and run:

```powershell
.\CookerPods-Diagnostic.exe --all-paired
```

This shows other paired names in the console only, not in the exported report. Additional options are `--seconds 10` through `--seconds 120`, `--no-wait`, and `--help`. Default invocation keeps the console open at the end until Enter is pressed. Exit code zero means the diagnostic finished and saved its report, not that Bluetooth or AirPods passed every check. Review `issues`, `adapter`, `scanStarted`, `watcherStopReason`, and `observations`.

## What it checks

- Windows default Bluetooth adapter and its reported LE/central-role support.
- Paired Classic Bluetooth devices and their OS connection status. Friendly-name matching is a heuristic, not authenticated AirPods identification or audio-routing confirmation.
- Passive Windows BLE manufacturer-data reception, separate from any GATT connection and from the Windows audio connection.
- A deliberately limited experimental decoder for the documented Apple type `0x07`, 25-byte proximity-message layout. Other lengths, versions, and invalid battery values stay unknown. Coarse values use ten-point steps; 0xF is unavailable, not zero.

Nearby candidates have temporary labels. Their addresses are used only in memory to group events, then discarded. **Candidates are not linked to your paired AirPods.** Rotating addresses may create several candidates for one device, or nearby devices may belong to someone else. The console/report therefore shows earbud **slot A/B**, not a claimed physical left/right mapping. Charging flags, encrypted battery data, ear-state automation, precise model names, and controls are not implemented.

Names, addresses, Windows device IDs, raw payloads, and exception messages are omitted from reports. Standard error type/HRESULT, OS version, process architecture, non-unique model code, scan counts, approximate fields, and timestamps remain. Up to 32 proximity candidates are retained; newer unrecognized frames clear older estimates. No unrelated Apple payload is retained. No-data results are inconclusive, not evidence of a zero battery or an unsupported entire platform.

## Hardware acceptance still required

No real AirPods or Windows radio is attached to the cloud build machine. A compiled preview and synthetic tests do not establish usable telemetry. Run on Windows 11 25H2 with the actual Intel adapter and AirPods Pro 2. Record adapter/driver, AirPods variant/firmware, case/earbud positions, playback state, and a near-time trusted battery reference separately. Compare case open/closed, charging, playback, sleep/resume, and a second nearby Apple device before enabling any real monitor feature. Do not use administrator privileges to mask an access-denied result; report its stage/code.

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

The Windows CI smoke check launches the packaged x64 executable with `--help`; it tests startup, not Bluetooth hardware. ARM64 is cross-published and its PE architecture checked; it requires a real ARM64 run for execution validation. No code signing or physical-hardware validation is claimed.

## Protocol provenance

The documented layout was inspected on 14 October 2026 in [Continuity's proximity message description](https://github.com/furiousMAC/continuity/blob/d9fa98bddafbfa3baafc84e37a417d78a0598546/messages/proximity_pairing.md). It cites Celosia/Cunche's [Discontinued Privacy research](https://petsymposium.org/2020/files/papers/issue1/popets-2020-0003.pdf). This implementation copies no dissector code or captured fixtures. The format document is historical and is not a current-firmware guarantee. In particular, earbud role/mapping and nearby-device association remain unverified.

Windows APIs: [BluetoothLEAdvertisementWatcher](https://learn.microsoft.com/en-us/uwp/api/windows.devices.bluetooth.advertisement.bluetoothleadvertisementwatcher), [BluetoothDevice](https://learn.microsoft.com/en-us/uwp/api/windows.devices.bluetooth.bluetoothdevice), and [BluetoothAdapter](https://learn.microsoft.com/en-us/uwp/api/windows.devices.bluetooth.bluetoothadapter). Official API metadata was read through Microsoft's documentation repository; actual unpackaged-app permissions and firmware behavior still need this hardware experiment.
