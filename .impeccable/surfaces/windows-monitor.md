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

This ordinary extension preserves incumbent DESIGN.md and .impeccable/design.json byte-for-byte and records implementation documentation here. No shipping rasters are used; synthetic review captures retain provenance.

Required evidence: actual WPF client-area captures from Windows CI, labeled synthetic, covering empty/light, populated picker/light, live/dark, stale/light and narrow/dark rendered at scale 2. This last capture does not establish physical 200% Windows desktop scaling. Linux cannot execute the Windows UI. No HTML/CSS detector applies to native WPF. Preserve incumbent DESIGN.md and its token sidecar; document this extension in this brief.

## Observed implementation

Documentation baseline: source commit `31c7c62bdf445d13545bd88f87ddc97dd7d8e208`, preview `0.5.0-native-preview`. PRODUCT.md remains product truth; `docs/native-windows.md` records user operation and hardware limits. Sources checked: `MonitorWindow.cs`, `MonitorRadio.cs`, `MonitorUiSmoke.cs`, `EntryPoint.cs` under `native/CookerPods.Diagnostic.Windows/`, and `native/CookerPods.Diagnostic.Core/LiveMonitorSession.cs`.

The executable opens a WPF Windows window by default. Its native caption says CookerPods; the client title says Battery monitor and carries an Experimental badge. Initial size is 700 × 650 DIP, minimum 480 × 460 DIP. A single rounded workspace swaps the anonymous candidate picker for three equal reading columns after the explicit Monitor selected action. Highlighting a row alone does not start monitoring; a sole row may be highlighted automatically, while the action remains explicit. Change candidate clears the selection and returns focus to the list. Pause scanning and Save report sit in a separate bottom row; body content has vertical scrolling.

The passive Windows Bluetooth watcher uses ordinary privileges and standard drivers, filters Apple manufacturer data rather than friendly names, and therefore does not exclude renamed AirPods. Nearby candidate labels are anonymous. Selection is a manual comparison with a trusted reading and does not authenticate ownership or associate a broadcast with Windows pairing/audio. There are no device settings, noise controls, charging flags or ear automation.

Live values are approximate ten-point estimates labeled Slot A, Slot B and Case. Physical left/right mapping remains unverified. Missing values show a dash and Unavailable, with no zero-value substitution. Freshness requires active scanning, a current-run packet and a decoded estimate at most 15 seconds old; unsupported new frames can leave an older estimate historical. Stale and paused values retain their age and historical labels while losing fresh emphasis. Resume waits for another packet. Candidates expire after two minutes, addresses can rotate, and reselection is explicit. Collection is bounded to 32 candidates, with oldest-seen eviction and no label reuse in a session.

Closing stops observation. Save report opens a Windows save dialog and writes local JSON only after a path is chosen. No friendly names, addresses, Windows identifiers or raw broadcasts are exported; no automatic upload or listening service exists. The default window does not perform diagnostic-mode paired-device metadata queries.

## Incumbent comparison and native adaptation

Compared the build with DESIGN.md, `.impeccable/design.json`, and the existing `styles.css` palette/component samples. The light/dark background, surface, text, muted, line, soft, blue, hover and focus values match the incumbent tokens. Flat tonal layering, a 16-DIP workspace radius, 10-DIP button corners, native disclosure and a single blue action accent preserve the connection-lab language. Original small WPF Geometry paths supply the earbud/case and window icons; no raster assets, downloaded fonts or decorative material ship.

The native surface adapts the browser hierarchy without changing its durable system: Segoe UI throughout; 28-DIP semibold window heading, 34-DIP semibold readings, 16-DIP state/selection, 15-DIP picker text, 14-DIP controls/slot labels, and 12–13-DIP supporting text. Native workspace padding and outer gutters are 24 DIP. Buttons retain a 44-DIP minimum height, with 14 × 8 DIP padding, a 3-DIP focus border and 0.55 disabled opacity. Candidate focus uses a 2-DIP border. The native badge has a 10-DIP radius; it does not redefine the browser badge token. Controls update without decorative animation or transitions. These are surface adaptations, not new global tokens or a replacement identity.

The implementation follows Windows app light/dark preference, reacts to preference changes, and substitutes system brushes in high contrast. WPF keyboard controls, accessible candidate/reading names, candidate help text, visible focus, selection focus transfer and polite state announcements are present in source. A per-monitor DPI manifest and native caption integration are implementation provisions, not proof of physical-display behavior. No formal accessibility conformance or screen-reader validation is established.

No pre-existing drift was identified in the sampled browser palette, type, radii and control definitions. Existing browser-specific DESIGN.md copy and the web sidecar remain scoped to their incumbent surface; native adaptations and newly authorized installation are recorded here and in PRODUCT.md rather than promoted into global rules. No pre-existing drift was repaired.

## Evidence and provenance

Evidence lives under `.impeccable/review/`. All five PNGs were opened during review and documentation and show their named states. Captures use actual Windows WPF controls rendered with `RenderTargetBitmap`, software rendering and an explicit fake radio/clock. The smoke fixture uses a 690-DIP window height, rather than the default 650-DIP application height. Result metadata records synthetic origin, source commit above and success; each image visibly says Synthetic fixture and embeds this PNG Description: `CookerPods WPF UI test capture; synthetic fixture, not AirPods hardware evidence.`

| Capture | Scene | Window width / render scale | Actual PNG pixels |
| --- | --- | --- | --- |
| `monitor-empty-light.png` | Empty picker; disabled action | 700 DIP / 1 | 684 × 651 |
| `monitor-picker-light.png` | Two candidates; highlighted row with explicit action | 700 DIP / 1 | 684 × 651 |
| `monitor-live-dark.png` | Fresh ~90% / ~100% / unavailable case | 700 DIP / 1 | 684 × 651 |
| `monitor-stale-light.png` | Historical 16-second sample; values are not live | 700 DIP / 1 | 684 × 651 |
| `monitor-narrow-dark.png` | Fresh reading at minimum window width | 480 DIP / 2 | 928 × 1302 |

`ui-smoke-result.json` records the capture matrix. Scale 2 doubles client-area rendering resolution; it is not a physical Windows desktop at 200%. Client-area evidence omits the native caption and window frame. The smoke path exercises real WPF choose/pause/resume/change controls with synthetic frames and asserts that resuming alone cannot freshen a sample. Synthetic mode is explicitly developer-invoked with `--ui-smoke`; ordinary startup uses the real radio.

Reported Windows CI runs [38694175586](https://github.com/CLOUDCOOKCH/CookerPods/actions/runs/38694175586) on main and [38694176248](https://github.com/CLOUDCOOKCH/CookerPods/actions/runs/38694176248) on the review tag passed 48 native tests, packaging and the synthetic WPF smoke. The evidence release tag `ui-review-0.5.0-31c7c62` publishes UI review images/result metadata only; it is not an application release. No HTML/CSS detector ran because its verdict does not apply to native WPF.

Earlier 0.4.0/0.4.2 ARM64 hardware observations and the user's independent rough battery confirmation justify trying this monitor; they do not validate the 0.5.0 window. The new window still requires a real ARM64 run covering reception, case changes, playback, pause/resume, sleep/resume and multiple nearby devices. Captures prove layout and synthetic control behavior, not identity, ownership, physical left/right, charging, audio association or continuous hardware reliability.

## Finish verdict and remaining boundaries

Disposition: **ship**, at the scope of the supplied synthetic WPF client-area UI evidence. `.impeccable/review/finish-review.md` contains the fresh delegated review using the shipped fallback finish-reviewer role instructions and all five required contract sections: persistence, fidelity, ceiling, material_fixes and keep. It reports no material fixes within that scope and retains anonymous deliberate choice, age/stale distinctions, unknown-case treatment and the restrained incumbent system.

There is no approved comp or independent QUALITY BAR board for this bounded code-led extension; no comp fidelity or broader visual-world comparison is claimed. The missing context launcher and inapplicable concept seed remain recorded in the direction contract. Review advisories remain: adjacent empty-state instructions repeat case-opening guidance; caption finish, minimum-height scrolling, physical desktop DPI, screen-reader behavior and Bluetooth hardware are uncaptured or unproven. These are not canonized as durable design rules and were not repaired in this documentation pass.

## Preserved system files

This ordinary-extension pass changes this surface brief only. DESIGN.md and `.impeccable/design.json` remain byte-for-byte unchanged; before/after SHA-256 values:

```text
bb4f93ed9697b0fc5c8321d9ed3026836ac224be196d21c4e2ba8943bdada1e7  DESIGN.md
e7ef2c5f77c0c8093f96e01d7ba4e80781b39d109d81e0c9cac512a4872393fb  .impeccable/design.json
```
