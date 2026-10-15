# AirPods noise controls on Windows: feasibility findings

Research date: 15 October 2026. Target: the user's renamed AirPods Pro 2, Windows 11 25H2 on a private ARM64 PC. Current requirements remain standard Windows Bluetooth drivers and ordinary app privileges. This is a source-based feasibility review, not a hardware test of noise-control commands.

## Result

ANC, transparency and related controls are technically possible on Windows through Apple's private accessory protocol. The Windows implementations inspected use an **additional Bluetooth kernel profile driver** to expose its L2CAP channel to a normal app. No verified route using only the standard Windows app-facing Bluetooth APIs was found.

That driver can sit alongside Microsoft's Bluetooth stack; it need not replace the whole Bluetooth adapter driver. It is nevertheless a custom driver and falls outside CookerPods' current requirements. The inspected NTPods driver targets x64, so it also does not supply the user's ARM64 control transport.

The existing 0.5.0 monitor remains useful without this driver. Receiving BLE battery advertisements, including during playback, does not establish a writable accessory-protocol connection.

## What the sources establish

| Question | Finding | Evidence |
| --- | --- | --- |
| Are AirPods noise-mode commands known? | Yes. Firmware-scoped protocol notes describe mode commands and responses over AAP on Classic Bluetooth L2CAP PSM `0x1001`. | [LibrePods AAP definitions][aap], explicitly scoped to AirPods Pro 2 USB-C firmware 7A305. The user's exact variant/firmware has not been established. |
| Can a normal Windows Winsock Bluetooth socket open that channel? | The documented Bluetooth socket protocol is `BTHPROTO_RFCOMM`, not a general Classic L2CAP socket. A known PSM is not an RFCOMM channel number. | [Microsoft Bluetooth socket reference][socket]. |
| Where does Microsoft document L2CAP channel creation? | In its Bluetooth profile-driver interface: a driver submits `BRB_L2CA_OPEN_CHANNEL`. | [Microsoft L2CAP client-driver documentation][l2cap]. |
| Is there a Windows implementation beyond a protocol document? | NTPods reports daily hardware use and implements a KMDF AAP driver plus user-mode client. Its driver README describes opening PSM `0x1001` and exposing operations through IOCTLs. These are the project's claims, not CookerPods hardware results. | [NTPods driver README][nt-driver], [channel-opening source][nt-channel], [user-mode driver bridge][nt-bridge] and [Windows README][nt-windows]. |
| Do other projects avoid the extra driver? | The inspected Airpods-Windows-Control project contains a kernel driver and BRB channel-opening code. AirPods GUI separates driver-free BLE monitoring from driver-dependent controls. MagicPods explicitly places genuine AirPods noise control under its MagicAAP driver features. | [Airpods-Windows-Control][old-readme], [channel-opening source][old-client], [AirPods GUI][gui], [MagicPods README][magic]. |
| Do the reviewed driver sources support ARM64? | NTPods' driver project defines x64 configurations and its INF declares `NTamd64`; no ARM64 configuration is present in those inspected files. Airpods-Windows-Control defines Win32/x64 configurations. This is not an exhaustive claim about every driver or future release. | [NTPods project][nt-project], [NTPods INF][nt-inf], [older driver project][old-project]. |
| Would signing the CookerPods EXE solve the channel limitation? | No. App reputation/signing and kernel-driver access are different issues. Public kernel-driver distribution has its own Microsoft signing requirements. | [Microsoft driver signing policy][signing]. |

The [AirPodsDesktop README][desktop] lists battery/ear-detection/audio features and explicitly excludes ARM64 official builds; it provides no evidence of a suitable ANC transport for this target. Android/Linux implementations likewise do not establish that their Bluetooth socket APIs exist in ordinary Windows apps.

## Evidence boundaries

- The Microsoft API documentation, driver project files and channel-opening source were read. No reference binaries, installers or device commands were run.
- NTPods' documentation describes working hardware control. Airpods-Windows-Control's README lists ANC testing on Windows 10 22H2 and AirPods Pro models. Neither is a test of CookerPods, this user's firmware, or an ARM64 driver.
- The NTPods and AirPods GUI installation notes describe test-signed drivers and changes to Windows boot/signing policy. Those are properties of the reviewed projects' current development route, not a universal requirement for every properly signed future driver. No such changes were made or prescribed here.
- An ordinary ARM64 application build does not provide an ARM64 kernel-driver package. A native driver needs its own build, installation metadata, signing and hardware validation; Microsoft's [ARM64 WDK guide][arm64] documents that separate build target.
- The private AAP service UUID described by NTPods is a Classic SDP/profile-driver binding. Its existence is not proof of a writable GATT characteristic or of a browser-accessible control service.
- Firmware-scoped command descriptions do not establish current mode readback, acknowledgement semantics or audio safety on the user's firmware. Receiving a reply would still need comparison with the actual listening mode.
- This review found no documented, demonstrated stock-driver path in the inspected sources. It does not prove that every future or undocumented Windows path is impossible.

## Consequence for CookerPods

Keep ANC/transparency controls out of the current standard-driver build. A button, parser, service UUID or socket wrapper would not create the missing transport. There is no new control-enabled executable to test from this research.

If the project later chooses an additional-driver architecture, the next concrete gate is **a properly distributed, compatible ARM64 profile driver that opens an AAP session on the actual PC while preserving normal audio**. Then verify exact firmware, current-mode notifications, one explicit reversible mode change and independent physical readback before enabling a control UI. Respect current-mode uncertainty and stop on failed/ambiguous readback. Driver support, signing, recovery and licensing must be resolved before an installation release; x64-only/test-signed examples are research references rather than a ready ARM64 dependency.

This would be a separately scoped driver project and a change to the current requirements. No driver or command code was copied into CookerPods. NTPods/LibrePods are GPL-3.0 projects; source access for research does not grant a permissive license to incorporate their implementation into the current MIT project. The older Airpods-Windows-Control repository has an MIT license, but its source is still used only as evidence here.

## Source snapshots

All links below pin the inspected revision so future agents can distinguish this result from later upstream changes.

[aap]: https://github.com/librepods-org/librepods/blob/efccc4499b2c1fd2ac243e975ae1f42d8e828ffd/docs/AAP%20Definitions.md
[socket]: https://github.com/MicrosoftDocs/win32/blob/ca8274decead49403d70ceeed4af8d4ca74eaf11/desktop-src/Bluetooth/bluetooth-and-socket.md
[l2cap]: https://github.com/MicrosoftDocs/windows-driver-docs/blob/3aed2ca9f3624d431cba4e74b74e2e7284edf718/windows-driver-docs-pr/bluetooth/creating-a-l2cap-client-connection-to-a-remote-device.md
[signing]: https://github.com/MicrosoftDocs/windows-driver-docs/blob/3aed2ca9f3624d431cba4e74b74e2e7284edf718/windows-driver-docs-pr/install/kernel-mode-code-signing-policy--windows-vista-and-later-.md
[arm64]: https://github.com/MicrosoftDocs/windows-driver-docs/blob/3aed2ca9f3624d431cba4e74b74e2e7284edf718/windows-driver-docs-pr/develop/building-arm64-drivers.md
[nt-driver]: https://github.com/arctumn/ntpods/blob/188e45df20259b616ce155be812086259d1a2488/windows/drivers/aap/README.md
[nt-windows]: https://github.com/arctumn/ntpods/blob/188e45df20259b616ce155be812086259d1a2488/windows/README.md
[nt-channel]: https://github.com/arctumn/ntpods/blob/188e45df20259b616ce155be812086259d1a2488/windows/drivers/aap/L2cap.c
[nt-bridge]: https://github.com/arctumn/ntpods/blob/188e45df20259b616ce155be812086259d1a2488/windows/daemon/src/driver.rs
[nt-project]: https://github.com/arctumn/ntpods/blob/188e45df20259b616ce155be812086259d1a2488/windows/drivers/aap/NTPodsAAP.vcxproj
[nt-inf]: https://github.com/arctumn/ntpods/blob/188e45df20259b616ce155be812086259d1a2488/windows/drivers/aap/NTPodsAAP.inf
[old-readme]: https://github.com/bho3538/Airpods-Windows-Control/blob/08477b452d673f4c820da169dc71c8ebe49509c8/README.md
[old-client]: https://github.com/bho3538/Airpods-Windows-Control/blob/08477b452d673f4c820da169dc71c8ebe49509c8/Airpod-Control-Driver/client.c
[old-project]: https://github.com/bho3538/Airpods-Windows-Control/blob/08477b452d673f4c820da169dc71c8ebe49509c8/Airpod-Control-Driver/Airpod-Control-Driver.vcxproj
[gui]: https://github.com/Lynder063/airpods-gui/blob/eaae390c9f953cb04b2784ae9eabb9c78257fc31/README.md
[magic]: https://github.com/steam3d/MagicPods-Windows/blob/d8fdbb3c3271718eeba6dde8d22b4c47029774aa/README.md
[desktop]: https://github.com/SpriteOvO/AirPodsDesktop/blob/56b04001f75044dbd15507303a9438c71e93c6e4/README.md
