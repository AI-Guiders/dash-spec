# DASHSPEC-ADR-0011: `@tab` modules and `tab … dashspec`

| | |
|---|---|
| **Status** | Accepted (updated surface — [ADR-0024](DASHSPEC-ADR-0024-document-authoring-layers.md)) |
| **Date** | 2026-06-24 |
| **Relates to** | [ADR-0010](DASHSPEC-ADR-0010-spec-ergonomics.md), [ADR-0024](DASHSPEC-ADR-0024-document-authoring-layers.md) |

## Context

Крупный dashboard (soak) и вкладка stakeholder дублировали shell. Tab module — отдельный `.dashspec`, parent ссылается через `tab … dashspec`.

Старый inner `tab id as "…" { filter … }` внутри module **удалён** — см. `standalone { }` / `filters { }` в [ADR-0024](DASHSPEC-ADR-0024-document-authoring-layers.md).

## Decision

### Ссылка из root dashboard

Внутри `report { }` parent dashboard:

```text
tab stakeholder as "Отчёты заказчика" dashspec "lus-dev-stakeholder.dashspec"
```

Путь — относительно каталога root `.dashspec`. Core **мержит** module во вкладку.

### Tab module file

```text
@tab stakeholder {
  runtime { manifest = "lus-runtime.toml" }
  configuration { sqldialect = tsql palette = "palettes/lus-apps.dashpalette" }

  !include "imports/stakeholder.dashinclude"

  wiring {
    use connector sqlserver
    use palette lus_apps
  }

  report {
    standalone {
      filter date usage_date on usage_date as "Дата отчёта" default -7d..today
      toolbar { usage_date user_name app_name }
    }

    filters {
      filter field period_grain on … as "Масштаб" ref G default day
      filter date period_start on period_start as "Период" ref P … grain_filter period_grain
    }

    card stakeholder_peak_apps as "№2 …" ref E {
      diagram lus_stakeholder_peak_apps_heatmap
      datasource view lus.v_daily_peak_concurrent_apps_per_user
      bind usage_date, user_name
    }
  }
}
```

| Блок | Standalone catalog entry | Embed в parent (`tab … dashspec`) |
|------|--------------------------|-----------------------------------|
| `runtime`, `configuration`, `wiring` | **Используется** | **Игнорируется** (shell parent) |
| `report.standalone { }` | filters + toolbar модуля | **Игнорируется** |
| `report.filters { }` | filters модуля | **Мержится** в parent |
| `report` → `card` | cards вкладки | **Мержится** |

**Embed-only minimal:**

```text
@tab stakeholder {
  !include "imports/stakeholder.dashinclude"

  report {
    filters { … }
    card … { … }
  }
}
```

### Standalone catalog entry

Файл `@tab id { … }` без parent — Host собирает одно-вкладочный dashboard:

- `id` / `title` = `@tab` id и optional `report "Title"` или `entry … as "Title"` в catalog;
- `standalone` + `filters` объединяются в filter set документа;
- cards → единственная вкладка `@tab` id.

### Merge rules

| Сущность | Правило |
|----------|---------|
| `filter` из `filters { }` | union; дубликат по `name` → ошибка |
| `card` | union; дубликат по `id` → ошибка |
| `tab.CardIds` | порядок `card` в module `report` |
| label вкладки | parent `tab … as "…" dashspec`; иначе catalog entry title |

## Consequences

- `Parse(text, specDirectory)` — обязателен при `dashspec` на tab.
- LUS: `lus-dev-stakeholder.dashspec` — `@tab { report { standalone filters card } }`.
- Inner `tab id as "…" { filter }` в module — **не парсится** ([ADR-0024](DASHSPEC-ADR-0024-document-authoring-layers.md)).
