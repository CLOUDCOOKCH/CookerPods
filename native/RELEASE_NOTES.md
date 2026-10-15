The previous preview rejected every proximity frame with a nonzero byte 10, and two full endpoint queries could delay observation by 20 seconds. This update corrects that historical-example check and starts broadcast capture before metadata enumeration.

- The format reference calls byte 10 undefined, showing zero as an example rather than a required discriminator. Nonzero suffixes now produce `DecodedWithNonzeroSuffix` and `hasNonzeroSuffix: true` when the documented prefix, length and battery ranges pass. Confidence explicitly notes the unverified firmware layout. The encrypted tail is ignored, not decrypted; invalid values and unsupported prefixes still remain unknown.
- Broadcast observation starts immediately, while metadata runs concurrently with a 20-second overall budget. `scanStartedAtUtc`, `metadataCompletion`, and `metadataSeconds` make this timing visible.
- Paired endpoint discovery now uses five-second DeviceWatcher windows, preserving partial entries instead of awaiting a full snapshot. Per-query completion and observation times distinguish completed, partial, canceled and prematurely stopped queries. Empty partial snapshots do not establish absence. Watcher state is bounded, stopped and detached; names/IDs remain local.
- Timestamped decode history and anonymous paired entries remain available. Counts do not establish unique physical devices or association with the user's AirPods.

Download **win-arm64** for a Windows ARM PC, or **win-x64** for an Intel/AMD PC. Extract the ZIP and run **CookerPods-Diagnostic.exe** with the existing Windows pairing. Open the case and take the earbuds out during the 30-second observation. Inspect and share the newly generated JSON from the local report path printed in the console. Nothing uploads automatically.

Validation includes synthetic decoder/privacy/concurrency tests, Windows builds, PE architecture checks, packaged x64 startup, and a time-limited Windows API/report smoke run without assumed Bluetooth hardware. Release downloads are checked against build metadata and SHA-256 checksums before publication. This updated ARM64 build still requires real-device testing, especially for frames with a nonzero suffix.

This remains an **unsigned, read-only diagnostic** with experimental, unassociated battery estimates. No separate .NET installation, administrator request, driver changes, browser bridge, ANC controls, ear automation, or settings writes are included. Managed-PC policy may require IT approval.

[Usage, protocol provenance and limitations](https://github.com/CLOUDCOOKCH/CookerPods/blob/v0.4.2-native-preview/docs/native-windows.md).
