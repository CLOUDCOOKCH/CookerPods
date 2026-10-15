# Windows battery monitor preview 0.5.0

This preview opens a **Windows app window** for experimental live battery estimates. It follows the approximate native readings independently confirmed on the user's Windows ARM64 PC.

- Choose a nearby candidate explicitly; renamed AirPods are not filtered out. Manual comparison does not authenticate ownership or Windows audio association.
- Read coarse earbud slot A/B and case values. Physical left/right is unverified; unavailable case values stay unknown.
- Estimates become stale after 15 seconds. Pause stops scanning; resume waits for a new packet. Address changes or two-minute expiry require a new choice.
- Save a local report on request. No names, addresses, device IDs, raw payloads or automatic uploads. No ANC controls, ear automation, driver replacement, browser helper or administrator request.

Download **win-arm64** for Windows ARM, or **win-x64** for Intel/AMD. Extract the ZIP and run **CookerPods.exe**. Open the case nearby, take the earbuds out if needed, compare readings and choose the matching candidate. The runtime is bundled. The preview remains unsigned; managed-PC policy may require IT approval.

Validation: 48 native tests; clean WPF build; self-contained packaging with runtime licenses/notices; Windows x64 diagnostic/API smoke and real WPF control tests with labeled synthetic light/dark/stale/narrow captures. ARM64 PE architecture is checked, but this new window still needs a real ARM64 hardware run. Prior ARM64 battery reports are feasibility evidence, not validation of this GUI or authenticated device identity.

[Usage and limitations](https://github.com/CLOUDCOOKCH/CookerPods/blob/v0.5.0-native-preview/docs/native-windows.md). The earlier diagnostic is available via `CookerPods.exe --diagnostic --no-wait`.
