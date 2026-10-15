# Windows AirPods controls extension

Mode: Operate. This is a bounded addition to the existing Windows monitor window and fixed DESIGN.md world. The user clarified that the goal is full AirPods management in Windows and chose noise-control work; the implementation now develops a driver-backed control path. Actual driver installation and hardware validation are distinct from building this source. No visual identity replacement or concept seed applies. The context launcher was already attempted in this conversation and is unavailable; incumbent context is read directly.

THESIS: Act on the explicitly chosen paired-device control service and show what it reports. A command is not a confirmed physical mode change.

OWN-WORLD: Inherit neutral light/dark surfaces, system Segoe UI, blue action/focus, flat 16-DIP workspace and 10-DIP controls. Preserve DESIGN.md and its sidecar.

STORY: Open Manage AirPods, choose an installed control service, connect, receive current state, then choose a noise mode or change a setting whose current state is known. Pending, unconfirmed, disconnected and unavailable states remain explicit. Nearby BLE selection is separate and never supplies control identity.

FIRST VIEWPORT: A disclosure inside the existing window switches its workspace from nearby battery monitoring to the control task. Connection and reported mode precede actions; a bounded two-column noise-mode action group carries the main task. Known boolean settings are actionable, unknown ones remain disabled. Device-reported battery and primary/secondary ear presence carry status. Native keyboard focus and quiet state updates take priority over decorative motion.

FORM: Local feature extension inside the incumbent composition, not a new whole app world. No comp, generated artwork or seed applies. Original small Geometry icons remain; no shipping rasters are introduced.

FINISH: unreviewed and undocumented is unfinished; this build ends with the finish review, the verdict, DESIGN.md, and every shipping raster carrying its provenance

Required review evidence: existing empty/picker/live/stale/narrow battery states plus connected, pending and narrow control states, actual WPF client-area captures with explicit synthetic provenance. A physical ARM64 driver session, hardware mode confirmation, native-caption appearance and physical DPI remain outside synthetic captures. No HTML/CSS detector applies to WPF. The unchanged incumbent system files and scoped implementation/evidence are documented after the fresh finish review.
