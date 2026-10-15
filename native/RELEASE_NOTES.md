The 0.4.0 preview could receive a supported battery frame and later export only `null` after an unsupported frame arrived. It also hid paired-device entries whose names did not contain AirPods. This preview makes both outcomes inspectable.

- Each proximity candidate reports its latest decoder outcome and counts of the first failed validation check. These reasons do not prove encryption or device identity.
- Up to eight decoded samples retain their original timestamps. A later unknown frame still clears the latest estimate; historical samples stay explicitly historical and may be stale.
- Paired discovery now supplements the Classic device-interface query with explicit paired Classic and LE association-endpoint queries. Unmatched names produce anonymous entries, with per-query counts and status sources. Queries can overlap; counts are not unique physical devices.

For a Windows ARM PC, download **win-arm64**. For Intel/AMD PCs, download **win-x64**. Extract the ZIP and run **CookerPods-Diagnostic.exe**. Keep the existing Windows pairing. For the first comparison, open the case and take the earbuds out during the 30-second observation. The console prints a local report path under `%LOCALAPPDATA%\CookerPods\reports`. Inspect the report before sharing it; nothing uploads automatically. `--all-paired` shows other paired names locally without exporting them.

Schema version is now **2**. Native tests cover decode outcomes, historical timestamps, unknown latest frames, callback ordering, bounded memory, and report privacy. Windows CI runs the packaged x64 executable with `--help`, checks both PE architectures, and verifies package checksums before publication. ARM64 is cross-published; execution of this updated preview needs hardware testing.

The previous preview ran on a user's ARM64 Windows PC and produced one unassociated battery estimate that roughly matched an independent reading. This supports further testing; it does not validate this release, device association, physical left/right mapping, case/charging behavior, or continuous monitoring.

This remains an **unsigned, read-only diagnostic**, using standard Windows APIs. No separate .NET installation, administrator request, driver changes, browser bridge, ANC controls, ear detection, or settings writes are included. Managed-PC policy can block unsigned previews; use your organization's approval process.

[Full usage and limitations](https://github.com/CLOUDCOOKCH/CookerPods/blob/v0.4.1-native-preview/docs/native-windows.md). Downloads include runtime licenses, source revision metadata, and SHA-256 checksums.
