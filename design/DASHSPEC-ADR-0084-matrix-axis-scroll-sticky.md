# DASHSPEC-ADR-0084: Matrix heatmap — sticky axis scroll

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-10-02 |

## Context

`matrix-canvas` heatmaps scroll inside `.matrix-canvas-scroll` (row cap / wide X). Y labels had partial `position: sticky` (left only); X labels scrolled away vertically, so users lost time/product context while scrolling the cell grid.

## Decision

- **Default:** `axis_scroll = sticky` on `heatmap { … }` — corner + X labels stick to top, Y labels stick to left inside the scrollport; only the canvas grid moves.
- **Opt-out:** `axis_scroll = none` (aliases `off`, `scroll`) — entire layout scrolls together (legacy).

```text
heatmap
  …
  axis_scroll = sticky
end heatmap
```

Presentation-only chrome (height, visible_rows) unchanged. Not tied to dashflow or time types.

## Non-goals

- Sticky axes when the **page** or **card-fullscreen** outer container scrolls (would need a split scrollport; follow-up if needed).
- Per-axis `axis_scroll_x` / `axis_scroll_y` in v1 (single knob is enough).
