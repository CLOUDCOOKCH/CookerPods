This portable Windows diagnostic checks paired-device status and passive Apple Bluetooth advertisements using standard Windows APIs. It is an early hardware experiment for AirPods Pro 2 on Windows 11, with no driver changes, administrator request, or separate .NET installation.

Download `win-x64` for an Intel/AMD Windows PC, or `win-arm64` for an ARM64 PC. Extract the ZIP and run **CookerPods-Diagnostic.exe**. Keep Bluetooth enabled and open the AirPods case nearby during the 30-second observation. The console prints the local JSON report path under `%LOCALAPPDATA%\CookerPods\reports`; inspect it before sharing. Nothing uploads automatically.

This is an unsigned development preview. Real AirPods hardware behavior is not yet validated. Nearby broadcast candidates are not associated with your paired AirPods, battery estimates are experimental, and physical left/right mapping is unverified. No ANC control, ear detection, browser connection, or continuous battery monitor is implemented.

Validation: 21 synthetic native tests and 23 browser tests; self-contained x64/ARM64 builds and architecture checks; packaged x64 startup with `--help` on Windows CI. ARM64 execution and Bluetooth hardware still need testing. The release job downloads packages from its successful build and verifies their checksums before publishing.

Downloads include runtime licenses, build metadata, and SHA-256 checksums. [Usage and limitations](https://github.com/CLOUDCOOKCH/CookerPods/blob/v0.4.0-native-preview/docs/native-windows.md).
