# NetStrata Design System

## 1. Atmosphere & Identity

NetStrata is a compact Windows diagnostic console: calm, technical, and evidence-led. The signature is the layered route map, where a single moving marker turns otherwise static probe results into an understandable sequence without implying packet capture. The existing HandyControl chrome remains the product foundation; Carbon-style data density and tonal layers inform the new diagnostic surface without adding a second UI framework.

Design read: operational desktop UI for developers and network troubleshooters. `DESIGN_VARIANCE: 4`, `MOTION_INTENSITY: 5`, `VISUAL_DENSITY: 7`.

## 2. Color

| Role | Resource or token | Light | Dark | Usage |
| --- | --- | --- | --- | --- |
| Window | `WindowBg` | `#F5F6F8` | `#0F1115` | Main background |
| Surface | `CardBg` | `#FFFFFF` | `#1A1D24` | Cards and nodes |
| Surface secondary | `RegionBrush` | HandyControl theme | HandyControl theme | Toolbars and grouped data |
| Text primary | HandyControl foreground | `#202124` | `#E8EAED` | Headings and body |
| Text secondary | `SecondaryTextBrush` / `Muted` | `#5F6368` | `#9AA0A6` | Metadata and hints |
| Border | `BorderBrush` / `CardBorder` | `#DADCE0` | `#2D323C` | Dividers and outlines |
| Interactive | `PrimaryBrush` / `Accent` | `#1A73E8` | `#8AB4F8` | Focus, active mode, moving marker |
| Passed | `NsFlowPassedBrush` | `#137333` | `#81C995` | Successful probe state |
| Degraded | `NsFlowDegradedBrush` | `#B06000` | `#FDD663` | Partial or degraded state |
| Failed | `NsFlowFailedBrush` | `#C5221F` | `#F28B82` | Failed probe state |
| Skipped | `NsFlowSkippedBrush` | `#5F6368` | `#9AA0A6` | Not executed |

Rules:

- Interactive blue is the only non-semantic accent.
- Status never relies on color alone; every node also displays a state word.
- New WPF surfaces use dynamic resources so light and dark themes remain equivalent.
- Raw colors are permitted only for semantic status resources declared by the component or theme service.

## 3. Typography

| Level | Size | Weight | Usage |
| --- | --- | --- | --- |
| Page title | 18 | SemiBold | Page heading |
| Section title | 15 | SemiBold | Diagnostic surface heading |
| Node title | 13 | SemiBold | Flow node label |
| Body | 12 | Normal | Explanations and reasons |
| Metadata | 11 | Normal | State, latency, timestamps |

- Primary font: `Microsoft YaHei UI`, inherited from the main window.
- Numeric metadata may use `Cascadia Mono, Consolas` when a fixed-width presentation improves comparison.
- Visible text is never smaller than 11 device-independent pixels.

## 4. Spacing & Layout

Base unit: 4 device-independent pixels.

| Token | Value | Usage |
| --- | --- | --- |
| `space-1` | 4 | Tight inline spacing |
| `space-2` | 8 | Node internal gap and compact controls |
| `space-3` | 12 | Card and toolbar gap |
| `space-4` | 16 | Page and panel padding |
| `space-6` | 24 | Major group separation |

- Existing main window minimum remains 720 by 560.
- Flow map uses a horizontal branched layout at 700 or wider and a vertical layout below 700.
- Nodes are at least 124 by 64, controls at least 36 high, and no label is clipped at the minimum window size.

## 5. Components

### Flow mode selector

- Structure: three native WPF buttons in one group.
- Variants: layered diagnosis, direct/proxy comparison, TLS stack.
- States: default, hover, pressed, keyboard focus, selected, disabled.
- Accessibility: selected button exposes a descriptive automation name and visible selected treatment.
- Motion: no automatic transition when switching modes.

### Flow node

- Structure: bordered surface, title, state word, optional latency.
- Variants: pending, active, passed, degraded, failed, skipped, unknown.
- States use text plus semantic stroke/fill. Failed and skipped nodes remain readable in grayscale.
- Accessibility: the automation name combines label, state, latency, and detail.
- Motion: active node uses one 160 ms opacity and scale emphasis; no looping pulse.

### Flow edge and marker

- Structure: static connection line plus one temporary circular marker.
- Variants: pending, active, passed, failed, skipped.
- Accessibility: edges are decorative; the live status line carries the same information in text.
- Motion: marker moves once per stage. It pauses, resumes, and disappears at completion.

### Playback toolbar

- Structure: play/pause/replay, reset, and speed selector.
- States: default, hover, pressed, focus, disabled while no data exists.
- Accessibility: native keyboard handling, visible focus, changing play label.
- Motion: button state changes are immediate.

## 6. Motion & Interaction

| Type | Duration | Easing | Usage |
| --- | --- | --- | --- |
| Micro | 140 to 180 ms | ease-out | Node state emphasis |
| Route segment | 220 to 900 ms | ease-in-out | Marker travel |
| Stage gap | 80 ms | linear | Readability between stages |

Segment duration maps measured latency with `clamp(220, 90 + sqrt(ms) * 45, 900)`. Missing latency uses 320 ms and never displays a fabricated number.

- The view never auto-plays on load, refresh, or mode change.
- Playback uses transform/position animations on a single marker and opacity on state emphasis.
- When Windows client-area animation is disabled, stages change discretely without marker travel.
- A new sample does not replace the trace while it is playing. The next refresh is applied after playback stops.

## 7. Depth & Surface

Strategy: tonal shift with restrained borders.

- Window, card, toolbar, and node hierarchy is expressed through existing HandyControl surface resources.
- Diagnostic nodes use a one-pixel outline because state and focus boundaries must remain explicit.
- No shadows on nodes or cards. Floating HandyControl menus may keep their framework elevation.
- Corner radii follow the existing application: 6 for compact controls, 8 for diagnostic nodes, 10 for major actions.

## 8. Accessibility Constraints & Accepted Debt

Constraints:

- Target WCAG 2.2 AA principles where applicable to desktop UI.
- All controls are reachable by keyboard with visible focus.
- State is expressed with text and color.
- The status line announces the current stage through WPF automation live settings.
- Windows reduced-animation preference is honored.
- Chinese and English labels are provided through the existing language resolver.

Accepted debt:

| Item | Location | Why accepted | Exit |
| --- | --- | --- | --- |
| Flow control uses code-driven Canvas layout | `NetworkFlowControl` | WPF has no built-in graph layout primitive and the topology is intentionally bounded | Extract a reusable layout service if more topologies are added |
| Visual regression is manual | WPF window | Repository has no screenshot test harness | Add deterministic screenshot tests when CI gains an interactive Windows runner |
