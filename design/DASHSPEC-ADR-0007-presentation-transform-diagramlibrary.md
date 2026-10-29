---
type: ADR
id: DASHSPEC-ADR-0007
title: "`presentation`, `transform` и `@diagramlibrary`"
status: "Accepted"
date: 2026-06-29
tags: [dashspec, adr]
language: ru
relates:
  - DASHSPEC-ADR-0002
  - DASHSPEC-ADR-0003
---

# DASHSPEC-ADR-0007: `presentation`, `transform` и `@diagramlibrary`

## Context

В `diagram line { x y series legend height max_series }` смешаны **привязка данных**, **трансформация** (top-N + Other) и **chrome рендерера** — ощущение «магии» ([обсуждение v0.3]).

## Decision

### Расслоение на уровне card

```text
card peak as "Peak concurrent" {
  diagram line {
    x = usage_date
    y = peak_concurrent_proxy
    series = app_name
  }
  presentation { use = line_bottom_300 }
  transform series { use = top5 }
  datasource view demo.v_daily_peak_concurrent_proxy
}
```

| Блок | Ответственность |
|------|-----------------|
| **`diagram`** | Только mapping колонок (`x`, `y`, `series`, heatmap `value`, …) |
| **`presentation`** | Chrome: `legend`, `height`, `stacked` |
| **`transform series`** | Top-N серий: `max`, `other` (метка «Other») |

Порядок слияния: **library preset → inline в блоке → legacy в `diagram` (deprecated) → defaults**.

### `@diagramlibrary`

```text
@config "demo.toml"
@sqldialect tsql
@diagramlibrary "demo-diagram-library.toml"
```

TOML с именованными пресетами:

```toml
[presentation.line_bottom_300]
legend = "bottom"
height = "300"

[transform.series.top5]
max = 5
other = "Other"
```

Ссылка: `presentation { use = line_bottom_300 }` или inline + override полей.

### Heatmap

- `height` для matrix — через **`presentation`**, не `diagram`.
- Подписи legend шкалы (`legend { min max }`) — по-прежнему на **card** ([ADR-0004]).

## Deprecated (пока парсится)

Свойства `legend`, `height`, `max_series`, `stacked` внутри `diagram` — fallback в `CardChromeResolver`.

## Non-goals v0.4

- `presentation` / `transform` на уровне dashboard (глобальные defaults)
- `transform` кроме `series` (pivot, binning)
- Валидация library TOML против kind (line vs heatmap)

## Consequences

- Demo sample: `@diagramlibrary` + пресеты в `demo-diagram-library.toml`.
- Именованные рецепты `[diagram.<id>]` (kind + chrome + bindings) — [ADR-0008](DASHSPEC-ADR-0008-viz-render-plugins.md).
- Новые presentation-свойства — схема `PropertySchemas.Presentation`, не registry kind.
