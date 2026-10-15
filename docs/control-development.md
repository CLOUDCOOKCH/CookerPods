# Driver-backed AirPods management: development status

The goal is full AirPods management in Windows. The 0.6 development source adds the first control milestone: a paired-device AAP channel, reported noise mode, mode-change requests, device-reported component battery, primary/secondary ear presence, and readback-gated ear-detection/conversation-awareness settings.

The public 0.5.0 release remains the independently tested battery monitor. This new control path is development work; a compiled driver and synthetic UI tests do not establish functioning controls on the user's AirPods. An installable control release requires a properly signed ARM64 driver and real hardware validation. No installer, certificate, boot-policy or Secure Boot change is included in this work.

## Components

- `native/CookerPods.Control.Driver`: original KMDF profile-driver source for ARM64 and x64. It binds the Classic AAP service, obtains its paired device address from Windows, fixes PSM to `0x1001`, bounds reads to 2048 bytes and permits only a small packet allowlist. One application owns a device channel. No arbitrary addresses, PSMs, kernel pointers, firmware writes or generic Bluetooth forwarding are exposed.
- `AapProtocol`: bounded parsing of noise, known boolean settings, component battery and primary/secondary ear state. Invalid, unsupported and metadata packets do not create device capabilities. Packet identities and raw bytes are not exported.
- `AapControlState` / `AapController`: explicit connection generation, command serialization, eight-second pending-command deadline, reports separate from requested values, disconnect/cancellation handling and no automatic command replay.
- `DriverAapTransport`: versioned fixed-width IOCTL client and bounded installed-interface enumeration. Handles and interface paths remain local. Its selected paired service is separate from any anonymous BLE candidate.
- `ControlPanel`: Manage AirPods inside the existing window. The current mode comes from device reports. Unknown settings stay disabled. Adaptive is enabled only after that mode has been reported in the current connection. A missing driver leaves ordinary battery monitoring available.

## State and physical verification

A successful write leaves the requested change pending. A matching subsequent device report changes its outcome to `DeviceReported`; this wording is intentional. The protocol does not supply an independent physical-mode measurement or a transaction identifier proving causality. A reply can describe a setting without proving its acoustic effect. Verify the actual ANC/transparency behavior on hardware.

If no recognized status arrives within ten seconds after initialization, the control session fails and closes. If no matching report arrives within eight seconds, the change is unconfirmed. A failed or interrupted connection disables writes. Reconnect uses a new session and discards old capability evidence; commands are never silently replayed. Known settings can be changed only after the device has supplied their current value. Ear presence retains primary/secondary roles rather than inventing a physical left/right mapping.

AAP component identifiers provide reported Left/Right/Case battery fields, distinct from the BLE monitor's unverified slot A/B estimates. A missing/disconnected component remains unknown. Reports and unsupported firmware still require independent hardware comparison. Ear-detection setting control is not host-side playback automation; pausing/resuming Windows media from ear events remains a separate feature. Conversation-awareness setting control likewise does not yet implement host-side Windows volume ducking.

## Driver build and distribution

The driver workflow restores pinned official WDK/SDK packages with normal NuGet verification, builds with 64-bit MSBuild for ARM64 and x64, verifies PE architecture and uploads explicitly unsigned development artifacts. Static analysis and compilation are development checks, not kernel lifecycle certification. The native workflow checks the shared ABI, core behavior, actual Windows interface enumeration, ordinary diagnostic behavior and synthetic WPF actions.

A normal installable driver package needs Microsoft's signing/distribution process, compatible architecture metadata and real-device testing. Signing the CookerPods application does not sign the kernel driver. No signing credentials are configured in this cloud session. The existing test-signed third-party examples are references rather than an installable dependency. See [driver source/build notes](../native/CookerPods.Control.Driver/README.md) and [transport research](noise-controls.md).

Before asking the user to load this driver, verify device removal, failed/timeout close, cancellation, power transitions and driver unload with Driver Verifier on an isolated Windows ARM64 test system. In particular, channel callback lifetime and failed teardown require kernel testing/review beyond compile checks. Validate normal Windows audio and recovery, exact AirPods variant/firmware, initial reported state and one explicitly requested reversible noise-mode change with physical readback. Do not present the development artifacts as a validated control release.

## Developer diagnostic

```powershell
.\CookerPods.exe --control-diagnostic
.\CookerPods.exe --control-diagnostic --connect-service 1
```

The first invocation only enumerates compatible installed interfaces and opens no device handle. The second explicitly initializes one service and observes reports for five seconds, without sending noise-mode or boolean-setting changes. Initialization sends the documented handshake, capability-enable and subscription messages. JSON is written to stdout; it excludes names, paths, addresses and raw packets. A GUI-subsystem executable may require a developer subprocess with captured stdout. The GUI's Save report exports control state while Manage AirPods is open; otherwise it exports the nearby battery monitor.

## Full-management scope

Noise-mode and supported-setting control is the first implementation milestone, with hardware confirmation still outstanding. Reliable reconnect, host-side ear/media automation, capabilities by verified firmware/model, accessibility validation, an install/uninstall/recovery path and production signing remain part of the product goal. Device rename, gestures, hearing-aid/calibration operations and arbitrary vendor commands are not implemented by this milestone. They need separate protocol evidence and verification; a settings screen is not proof of support.
