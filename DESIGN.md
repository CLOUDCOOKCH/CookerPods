---
name: CookerPods connection lab
description: Restrained, accessible presentation for a read-only browser transport diagnostic.
colors:
  bg: "#f5f5f7"
  surface: "#fff"
  text: "#202124"
  muted: "#62636a"
  line: "#dcdce0"
  blue: "#0066cc"
  blue-hover: "#0055aa"
  soft: "#e9e9ee"
  focus: "#0071e3"
  selection-bg: "#b8d8ff"
  selection-text: "#152c47"
  button-text: "white"
  dark-bg: "#141416"
  dark-surface: "#202023"
  dark-text: "#f5f5f7"
  dark-muted: "#b0b0b9"
  dark-line: "#43434a"
  dark-soft: "#35353c"
  dark-blue-hover: "#005bb8"
  dark-focus: "#80b9ff"
typography:
  display:
    fontFamily: 'system-ui, -apple-system, "Segoe UI", sans-serif'
    fontSize: "clamp(38px, 6vw, 64px)"
    fontWeight: 650
    lineHeight: 1.06
    letterSpacing: "-.035em"
  title:
    fontSize: "23px"
    fontWeight: 650
    lineHeight: 1.3
    letterSpacing: "-.025em"
  body:
    fontFamily: 'system-ui, -apple-system, "Segoe UI", sans-serif'
    fontSize: "16px"
    lineHeight: 1.6
  label:
    fontSize: "14px"
    fontWeight: 600
  hint:
    fontSize: "13px"
  badge:
    fontSize: "12px"
    fontWeight: 600
rounded:
  button: "10px"
  workspace: "16px"
  badge: "20px"
spacing:
  action-gap: "10px"
  compact: "12px"
  section-gap: "16px"
  gutter-mobile: "20px"
  group: "24px"
  gutter-desktop: "32px"
components:
  button-primary:
    backgroundColor: "{colors.blue}"
    textColor: "{colors.button-text}"
    typography: "{typography.label}"
    rounded: "{rounded.button}"
    padding: "12px 18px"
  button-primary-hover:
    backgroundColor: "{colors.blue-hover}"
  button-secondary:
    backgroundColor: "{colors.soft}"
    textColor: "{colors.text}"
    typography: "{typography.label}"
    rounded: "{rounded.button}"
    padding: "12px 18px"
  button-secondary-hover:
    backgroundColor: "{colors.line}"
  workspace:
    backgroundColor: "{colors.surface}"
    rounded: "{rounded.workspace}"
    padding: "32px"
  badge:
    backgroundColor: "{colors.soft}"
    textColor: "{colors.muted}"
    typography: "{typography.badge}"
    rounded: "{rounded.badge}"
    padding: "5px 11px"
---

# Design System: CookerPods connection lab

## Overview

**Creative North Star: "Connection lab"**

This records the implemented diagnostic, using its existing label as the organizing description. The confirmed direction is restrained Apple-inspired materials and system typography: a quiet page, a tonal workspace, clear actions, and explicit evidence. No new visual identity or full manager design is established here.

**Key Characteristics:**

- Tonal surfaces with generous outer space and compact diagnostic rows.
- System light/dark appearance and a single blue action accent.
- Native controls, visible focus, plain-language status, and honest unavailable states.

## Colors

### Primary

Blue marks the main action and native checkbox accent. Its hover token deepens in each color scheme. Focus has a separate, high-visibility token. Button text remains white in both schemes.

### Neutral

The page background supports a contrasting workspace surface; text and muted text establish hierarchy, while line and soft tokens define separators and secondary controls. Tokens prefixed `dark-` replace their corresponding light values under `prefers-color-scheme: dark`. Blue, button text, and selection colors remain unchanged. Selection uses its explicit foreground/background pair.

## Typography

All text inherits the body system stack, including buttons; there are no downloaded fonts. The display role is the balanced introductory heading, title is the section heading, label is action/strong-row text, and hint and badge provide quieter detail. Device headings use a separate observed size (20px) and inherited bold weight. Introductory copy is larger (20px, becoming 18px at the mobile breakpoint). Paragraphs cap at 72ch; introductory copy caps at 52ch. Readings use tabular numerals.

## Layout

This surface is a single column. Header/footer cap at 1040px; main caps at 880px, including padding. Main uses desktop padding (45px 32px 20px); the intro caps at 690px. The workspace uses its frontmatter padding. Headings, actions, and header/footer can wrap. Diagnostic rows align labels left and values right; readings cap values at 55% and wrap long device text.

At 580px and below, main padding becomes 26px 20px 0, header/footer use 20px padding, workspace padding becomes 24px 20px, intro separation reduces from 46px to 30px, and the brand subtitle moves to its own line. This is responsive web behavior, not a native mobile surface.

## Elevation & Depth

The implementation uses flat tonal layering with no shadows, blur, or floating surfaces. Thin rules separate diagnostic rows, device details, disclosures, and footer. Buttons transition only background color over 150ms with `ease-out`; reduced-motion preference removes that transition.

## Shapes

Workspace, buttons, and status badges use the three rounded values above. Native checkbox geometry remains browser-provided. Buttons have a minimum height (44px); badges communicate state without looking like controls.

## Components

### Buttons

Primary blue starts device selection. Soft secondary buttons handle disconnect, report download, optional broadcasts, and serial inspection. Hover uses the corresponding hover token; disabled controls use half opacity and a `not-allowed` cursor. Keyboard focus is a 3px focus outline offset by 4px. No additional pressed-state treatment exists.

### Workspace and diagnostic rows

The rounded surface contains capability rows, optional broadcast consent, action buttons, live status, and a selected-device panel. The selected panel appears only after selection. Capability, reading, and connection states are expressed in text; a device name is plain text and may wrap.

### Native controls and disclosure

The optional broadcast checkbox is 18px square with the blue accent, associated label, and hint. Native details/summary elements reveal serial inspection and interpretation guidance. Both retain native keyboard interaction and the shared focus outline. Status messages use polite live regions.

## Do's and Don'ts

### Do:

- Do preserve system fonts, system color preference, reduced-motion behavior, and visible keyboard focus.
- Do distinguish API availability, selection, GATT connection, one-time readings, and verified device capabilities in text.
- Do allow device text and controls to wrap on narrow screens.

### Don't:

- Don't represent mocked screenshots or a connected badge as evidence of AirPods hardware support.
- Don't add fabricated live readings or controls to imply a functional manager.
- Don't require new imagery, downloaded fonts, installations, or experimental browser flags for this diagnostic.
