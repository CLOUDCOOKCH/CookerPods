# Browser-only experiment

Historical browser-only scope: the user has since explicitly authorized a native Windows experiment after unsuccessful GATT connections. See [the native preview](native-windows.md). The API restrictions and browser results below remain relevant; the native path is now an explicit alternative, not an implicit replacement. Native [hardware observations](hardware-validation.md) and [Windows noise-control research](noise-controls.md) record subsequent evidence and constraints.

Scope: Windows 11 25H2, current Edge, AirPods Pro 2, existing Intel Bluetooth driver. No installed bridge, browser flags, or custom drivers. No physical hardware has been tested in the cloud workspace.

## Why this experiment

[Ear-web](https://github.com/radiance-project/ear-web/blob/d076e1371572fbf0a14b441f9b3cafbf9a237e04/res/js/bluetooth_socket.js) uses Web Serial with a Nothing-specific Bluetooth service UUID. [LibrePods' firmware-scoped AAP notes](https://github.com/librepods-org/librepods/blob/efccc4499b2c1fd2ac243e975ae1f42d8e828ffd/docs/AAP%20Definitions.md) describe L2CAP PSM `0x1001`. [Web Bluetooth](https://github.com/WebBluetoothCG/web-bluetooth/blob/main/index.bs) exposes permitted GATT services rather than arbitrary Classic L2CAP sockets. A ported parser alone cannot supply a missing browser transport.

The probe requests `battery_service` and `device_information`, reads `battery_level`, `model_number_string`, and `firmware_revision_string`, and never writes characteristics. Optional Apple manufacturer-data permission (`0x004c`) enables only a short selected-device broadcast observation where the browser supports it. No decryption keys or raw data are requested from another source. Device discovery is constrained by browser permissions: this is not exhaustive private-service enumeration.

## Hardware procedure

Record the precise browser/Windows/adapter/driver and AirPods variant/firmware manually. Run at least three sessions: case open/closed, buds inside/outside, one bud present, charging/not charging, playback, Bluetooth off/on, and sleep/resume. Check interference with audio and another nearby Apple device. Compare any battery value with a trusted near-time display; don't assume the standard value belongs to a particular component.

Save reports before refreshing. Reports omit identifying device properties but include standard model/firmware strings if read. Note which physical device you selected and the experiment conditions separately. A no-result run is still useful evidence; it must not be converted into a fake battery or broad impossibility claim.

## Gate for further implementation

Proceed to real monitoring only when an actual browser-accessible source produces useful, correctly associated, repeatable telemetry without installation or flags. Suggested monitor acceptance: first reading within 60 seconds and no unexplained gap over five minutes in three 15-minute listening sessions, with declared accuracy against a reference. The current probe takes one-time readings; a subsequent experiment must establish notifications or permitted repeated reads before claiming a monitor.

Controls require a separately demonstrated command transport and physical readback. If no browser path works, document the blocker and keep the browser-only constraint. A native companion is not an implicit fallback, and a polished mock is not a functional MVP.
