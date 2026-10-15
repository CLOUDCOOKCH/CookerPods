# CookerPods 0.6 development build

This ZIP contains the Windows application and its runtime. It is an unsigned development build, not a validated AirPods control release. Read `BUILDINFO.json` for its exact source revision and architecture. Keep the license and notice files with the app.

Extract the entire ZIP and run `CookerPods.exe`. Use **win-arm64** on Windows ARM or **win-x64** on Intel/AMD. No separate .NET installation is needed. Managed-PC policy may require IT approval.

## Battery monitoring

Open the case nearby, then take the earbuds out if no estimates appear. Compare the anonymous candidate readings against a trusted battery reference before choosing **Monitor selected**. A nearby broadcast is not authenticated as your AirPods or associated with Windows audio. Slot A/B do not establish physical left/right. A dash means unavailable, not zero.

Estimates become stale after 15 seconds. **Pause scanning** stops observation; resuming waits for a new packet. Use **Change candidate** if the selection expires or the device changes address. **Save report** writes a local JSON file only when requested. Closing the app stops observation and releases its control session. Reports omit names, addresses, interface paths and raw Bluetooth packets; nothing uploads automatically.

## Manage AirPods: development controls

This build includes a driver client and interface for noise modes, device-reported battery/ear presence, and supported ear-detection/conversation-awareness settings. **The required kernel profile driver is not included or installed by this ZIP.** Without a compatible installed driver, the app reports that controls are unavailable; nearby battery monitoring remains available.

The driver source builds for ARM64 and x64, but normal distribution still requires Microsoft signing and isolated kernel/hardware validation. Do not change Windows security or boot policy to try these artifacts. Signing the application would not sign the driver.

Control services are selected separately from nearby battery candidates. A successful command write is not confirmation: the interface retains the previous reported value while waiting for a matching device report. Unknown settings remain disabled, and Adaptive needs a report of that mode in the current connection. Physical effects and current firmware compatibility are unverified. Windows media automation, conversation-triggered host volume changes, rename and gesture customization are not implemented.

Full development status and remaining gates: https://github.com/CLOUDCOOKCH/CookerPods/blob/main/docs/control-development.md

The independently tested battery-only 0.5.0 preview remains available at https://github.com/CLOUDCOOKCH/CookerPods/releases/tag/v0.5.0-native-preview . This development ZIP does not replace that release.
