# DASHSPEC-ADR-0030: Report scale — `page`, `gate`, `phase`, catalog `group`

| | |
|---|---|
| **Status** | Proposed |
| **Date** | 2026-07-05 |
| **Relates to** | [ADR-0023](DASHSPEC-ADR-0023-dashcatalog.md), [ADR-0024](DASHSPEC-ADR-0024-document-authoring-layers.md), [ADR-0028](DASHSPEC-ADR-0028-bounded-card-click-interactions.md), [ADR-0029](DASHSPEC-ADR-0029-inspect-tooltip-presentation-split.md), [ADR-0031](DASHSPEC-ADR-0031-display-vocabulary-no-as.md) |

## Context

Dev/prod-like объёмы (97+ CAD × 500+ users × месяцы) ломают отчёты, собранные как **tab → N cards** на одной сетке:

- heatmap `user × day` без выбранного пользователя;
- horizontal bar «DAU по всем продуктам»;
- overview, смешивающий dev soak и fleet seed.

**Top-N / scroll** — presentation workaround; данные и **аналитический сценарий** в spec не выражены.

Иерархия `catalog entry → @dashboard → report → tab → card` покрывает layout, но не:

1. **один аналитический вопрос** (= один экран с локальным layout);
2. **precondition** («рисовать только когда user выбран»);
3. **browse → detail** как явная фаза;
4. **группировку entry** в catalog picker (без naming conventions).

Display strings: [ADR-0031](DASHSPEC-ADR-0031-display-vocabulary-no-as.md) — `title` / `label` / `message` / `format`, без `as`.

## Decision

Три дополнения к **report grammar** + **`group` в catalog** ([ADR-0023](DASHSPEC-ADR-0023-dashcatalog.md)).

### 1. `group` в `.dashcatalog`

```text
@catalog lus_prod

default = peak_by_app

group stakeholder {
  title = "Заказчик"

  entry peak_by_app {
    title = "№1 Пик по ПО"
    dashspec = "lus-stakeholder-peak-by-app.dashspec"
  }

  entry user_peak_apps {
    title = "№2 Разных ПО у пользователя"
    dashspec = "drafts/lus-stakeholder-report2-pages.dashspec"
  }
}

group operations {
  title = "IT / качество"

  entry detail {
    title = "Детализация"
    dashspec = "lus-dev-detail.dashspec"
  }
}

entry soak {
  title = "Dev Soak"
  dashspec = "lus-dev-soak.dashspec"
}
```

| Правило | |
|---------|--|
| `group` | секция picker; `title` на group и entry ([ADR-0031](DASHSPEC-ADR-0031-display-vocabulary-no-as.md)) |
| Ungrouped `entry` | backward compat |
| `default` | id **entry**, не group |

### 2. `page` — аналитический экран внутри `report`

```text
@tab stakeholder {
  report {
    standalone { … }

    page peak_by_app {
      title = "№1 Пик одновременности по ПО"
      !include "layouts/stakeholder-peak-by-app.dashlayout"
      card peak_by_app ref Q { … }
    }

    page user_peak_apps {
      title = "№2 Разных ПО у пользователя"
      …
    }
  }
}
```

**Prod 1:1:** catalog `entry { title = … }`; inner spec — **id = entry id**, `title` omitted on page/card ([ADR-0031](DASHSPEC-ADR-0031-display-vocabulary-no-as.md)).

### 3. `gate` — precondition карточки

```text
card user_day_heatmap ref D {
  gate requires user_name {
    message = "Выберите пользователя"
  }
  diagram …
}

card browse_top_users ref B {
  gate when user_name.empty
  on click { set user_name from y }
}
```

### 4. `phase` — browse / detail

```text
page user_peak_apps {
  title = "№2 …"

  phase browse {
    card browse_top_users ref B { … }
  }

  phase detail {
    card user_day_heatmap ref D {
      on click { show below list data from tooltip copy }
    }
  }
}
```

### 5. `presentation viewport` (P3)

Scroll/pagination без отбрасывания данных.

## Hierarchy (target)

```text
@catalog
  group? { entry → @tab | @dashboard }
  report
    tab? / page / phase? / card (+ gate, on click)
```

## LUS reference

Draft: `URSA.LicenseUsage/docs/dashspec/drafts/lus-stakeholder-report2-pages.dashspec`.

## Implementation phases

| Phase | Deliverable |
|-------|-------------|
| **P0** | ADR-0030 + ADR-0031 + drafts |
| **P1** | `page`, `gate`, `group`; display vocabulary parser |
| **P2** | `phase`, `goto page` / `goto entry` |
| **P3** | viewport presentation |

## Consequences

- **ADR-0023:** `group { entry { title = … } }`
- **ADR-0031:** remove `as`; examples here use block `title` / `label` / `message` / `format`
