# DASHSPEC-ADR-0055: Gantt timeline renderer (`gantt-timeline`)

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-09-25 |
| **Relates to** | [ADR-0033](DASHSPEC-ADR-0033-plugin-families-and-microkernel-host.md), [ADR-0030](DASHSPEC-ADR-0030-report-scale-pages-gates-and-suites.md) |

## Context

`gantt-html` — компактный activity timeline (label + bar track). Подходит для «какие приложения были активны», но не похож на PM-Гант: нет grid с датами/длительностью, tiered time scale, today-line, split-pane scroll.

Stakeholder LUS (`stakeholder-user-app-gantt`) и будущие schedule-дашборды нуждаются в **настоящем** timeline UX без смены SQL/payload pipeline.

## Decision

### Два рендерера, один payload

| Renderer | Id | Назначение |
|----------|-----|------------|
| **Activity strip** | `gantt-html` | компактные полоски (legacy, без изменений) |
| **Timeline Gantt** | `gantt-timeline` | split grid + ruler + scroll |

Оба используют `GanttPayload` из `GanttPayloadBuilder` (`from`/`to`/`y`/`color`, `axis_from`/`axis_to`/`step`).

### UI (v1)

```text
┌─────────────────────────────────────────────────────────────┐
│ Название │ Начало      │ Конец       │ Длит. │ ▓▓▓ ruler ▓▓▓│
├──────────┴─────────────┴─────────────┴───────┼─────────────┤
│ AutoCAD  │ 09:05       │ 09:10       │ 5м    │ ████        │
│ Revit    │ 09:15       │ 09:20       │ 5м    │     ████    │
└──────────────────────────────────────────────┴─────────────┘
         ↑ sticky sidebar                    ↑ today marker (if in range)
```

- **Grid:** label, start, end, working duration (span min→max segment per row).
- **Ruler:** auto grain — час (≤24h), день (≤14d), неделя (иначе).
- **Scroll:** `visible_rows` из presentation chrome (preset `gantt_timeline`, default 8).
- **Today:** вертикальная линия, если «сейчас» внутри `[axis_start, axis_end]`.

### Spec

```text
chrome
  use gantt_timeline
end chrome

gantt
  render = "gantt-timeline"
  y = app_name as "ПО"
  from = segment_start_utc
  to = segment_end_utc
  ...
end gantt
```

Preset `gantt_timeline`: `height = 420`, `visible_rows = 8`.

## Non-goals (v1)

- Иерархия parent/child, summary brackets, milestones (ромб).
- Dependency arrows (FS/SS).
- Drag-resize, assignee avatars.
- Refetch / отдельный SQL для Gantt.

## Consequences

- LUS stakeholder card переключается на `gantt-timeline` + `gantt_timeline` chrome.
- `gantt-html` остаётся для простых embed.
- Follow-up ADR: row `group` / `parent` для иерархии, dependencies layer.
