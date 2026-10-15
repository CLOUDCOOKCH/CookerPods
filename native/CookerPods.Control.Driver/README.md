# CookerPods control profile driver (development)

This original KMDF driver opens the Classic Bluetooth AAP service at PSM 0x1001. It is bound to the service PDO, obtains that device's address from Windows and exposes a fixed x64/ARM64-neutral IOCTL ABI. A client cannot supply arbitrary radio addresses, PSMs, kernel pointers or general Bluetooth write packets. One client owns a device at a time. The current allowlist covers handshake/subscription, noise modes, ear-detection and conversation-awareness settings; it provides no firmware-writing or hearing-aid operations.

This is development source and **not an installable, production-signed driver release**. Compilation does not validate kernel lifecycle, audio interoperability, hardware command response or current firmware. A Microsoft-signed ARM64 package and isolated real-device/Driver Verifier testing are prerequisites to a normal-user release. The application EXE's signature cannot substitute for kernel-driver signing. No installer or script changes boot policy, Secure Boot, certificate stores or the user's Bluetooth setup.

## Build

Use Windows, Visual Studio 2022 C++ x64/ARM64 tools and Spectre libraries, and NuGet. The pinned official WDK/SDK NuGet dependencies are restored with their normal integrity/signature verification:

```powershell
nuget restore native/CookerPods.Control.Driver/packages.config -PackagesDirectory artifacts/wdk-packages -NonInteractive
msbuild native/CookerPods.Control.Driver/CookerPodsControl.vcxproj /p:Configuration=Release /p:Platform=ARM64
```

Use `x64` for the second architecture. Build outputs stay under ignored `artifacts/`. The development workflow compiles and checks PE architecture; it does not load or install the driver on the hosted runner. Unsigned files are review artifacts, not a request to bypass Windows policy.

## API provenance

Implementation is original MIT-licensed CookerPods code. Microsoft Bluetooth DDI and KMDF documentation and the architecture of the Microsoft Bluetooth Echo sample were consulted; no NTPods/LibrePods implementation was copied. Protocol facts are referenced in [noise-control research](../../docs/noise-controls.md). No clean-room claim is made.

Microsoft reference snapshot: Windows-driver-samples commit `2dc3fd3a0cc84a2933f2194e7ec0871584979071`, `bluetooth/bthecho/bthcli/sys/` and the documented `IOCTL_INTERNAL_BTHENUM_GET_DEVINFO`, `BTH_PROFILE_DRIVER_INTERFACE`, `BRB_L2CA_OPEN_CHANNEL`, `BRB_L2CA_ACL_TRANSFER` and close-channel operations.
