# Native hardware observations

Updated 15 October 2026. These summaries record user-supplied reports and separately stated test conditions. Raw reports, radio identifiers and payloads are not committed. Synthetic Windows CI captures are separate evidence about UI controls/layout.

## Setup and scope

The user reported a private Windows ARM64 PC, Windows 11 build 26200, renamed AirPods Pro 2 paired/connected through the existing Windows Bluetooth stack. The live-monitor export does not itself identify CPU architecture, exact AirPods variant/firmware, playback route, case position or trusted-reference device. Those conditions must not be inferred from a broadcast.

Earlier 0.4.0 and 0.4.2 diagnostic readings roughly matched independent battery references. Version 0.5.0 added the live WPF window and was then executed in the user's hardware-testing context.

## Version 0.5.0 results

Times below are local Europe/Berlin time (CEST, UTC+02:00) on 15 October 2026.

| Export | Reported observation | Independent context |
| --- | --- | --- |
| 09:05:32 | `Live`, scanning, no scan issue; selected candidate 1; 60 decoded frames; newest sample age 0.9 s; slot A/B approximately 100%/100%, case approximately 70%. All eight retained samples agree over 14.51 s. Two nearby candidates report matching values. | User explicitly confirmed all three estimates roughly match a trusted battery reading. Exact reference device/value and firmware are not recorded. |
| 09:09:20 | `Live`, scanning, no scan issue; selected candidate 3; 68 decoded frames; newest sample age 0.1 s; slot A/B approximately 100%/100%, case unavailable. Eight retained samples span 15.58 s. | User said they played a YouTube video after being asked to listen through AirPods with the case closed. The export does not verify audio routing or case closure; its snapshot is evidence at export, not a full playback-session recording. |

Both selected candidates carry model code `0x2420`, including frames flagged for a nonzero undefined suffix. This is not authenticated identity or independent confirmation of a firmware layout. The first report counts 15 `Decoded` and 45 `DecodedWithNonzeroSuffix` frames; the second counts 3 and 65 respectively. Retained histories describe only the last eight samples, not all earlier decoded values.

## What this supports

The 0.5.0 app executes in the user's Windows ARM64 testing context, receives repeated proximity broadcasts and exports a fresh selected reading. Coarse values, including one case estimate, have independent user confirmation. A subsequent playback test still yielded fresh earbud readings at export. Renaming did not prevent these observations.

## What remains unverified

- Physical ownership, authenticated model identity and physical left/right mapping.
- Whether matching candidate labels represent one pair, several components or different nearby devices. Matching values do not justify automatic reassociation. Selection changed between reports; the cause and any manual reselection are not recorded.
- Uninterrupted monitoring across a long listening session, radio-address changes, Bluetooth off/on, sleep/resume or multiple nearby Apple devices.
- Battery accuracy across discharge/charging and different percentages. A missing case value means unknown, not zero; its cause is unproven.
- Noise-mode control or readback. Passive battery reception does not establish a control transport; see [noise-control research](noise-controls.md).
- Real-device pause/resume and stale-state transitions. Synthetic tests cover these logic/control paths, but these two exports do not demonstrate them on hardware.

Further real tests should have a defined duration and record the physical conditions, whether selection changed, and a near-time trusted reference. Acceptance of an experimental manually selected monitor must not be described as validation of seamless device management.
