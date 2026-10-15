# Windows monitor window

Mode: Operate. Target: native/CookerPods.Diagnostic.Windows/MonitorWindow.cs and MonitorRadio.cs.
The user chose a Windows app window after confirming that approximate earbud readings match an independent source and that their AirPods were renamed. Selection must not depend on a friendly name. This is a scoped extension of the established connection-lab composition and visual language, not a new visual world. The context launcher is unavailable; source context was read directly. No comp or seed was generated for this bounded native extension.

## Direction contract

THESIS: Make a nearby battery estimate useful without implying ownership, Windows audio connection, or device control. Explicit selection precedes monitoring.

OWN-WORLD: Inherit DESIGN.md's neutral surfaces, blue action/focus accent, system typography, restrained rounded workspace and native affordances. Use the Windows system light/dark preference; this window lives alongside work apps in changing ambient light.

STORY: Start scanning, compare nearby readings, choose one candidate, then read three approximate values. A changing address requires a new choice; age and pause status stay visible.

FIRST VIEWPORT: A native caption and a modest title lead into one workspace. Before selection it contains a bounded candidate list and the primary selection button. After selection, the same space holds three labeled battery readings and a clear freshness line. Pause and report actions remain reachable at narrow widths. Signature interaction: choosing a candidate replaces the picker in place with its measured reading, preserving the window's calm structure; pauses and stale data visibly stop claiming freshness. Native focus and system motion preferences take priority over decorative animation.

FORM: Scoped native extension of the existing single-column diagnostic workspace. No concept seed applies; the missing launcher and the user-selected Windows surface are recorded above. No new brand system or invented hardware claims.

FINISH: unreviewed and undocumented is unfinished; this build ends with the finish review, the verdict, DESIGN.md, and every shipping raster carrying its provenance

Required evidence: actual WPF captures from Windows CI, labeled synthetic, covering empty/light, live/dark, stale/light and narrow/200-percent layouts. Linux cannot execute the Windows UI. No HTML/CSS detector applies to native WPF. Preserve incumbent DESIGN.md and its token sidecar; document this extension in this brief.
