# DASHSPEC-ADR-0011: `@tab` modules and `tab … dashspec`

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-06-24 |
| **Relates to** | [ADR-0010](DASHSPEC-ADR-0010-spec-ergonomics.md) |

## Context

Крупный dashboard (soak) и вкладка stakeholder дублировали оболочку (`@config`, toolbar, фильтры). Нужна модульность: вкладка живёт в отдельном файле, корень ссылается на неё.

## Decision

### Корневой файл — `@dashboard`

```text
tab stakeholder dashspec "lus-dev-stakeholder.dashspec"
tab stakeholder as "Отчёты заказчика" dashspec "lus-dev-stakeholder.dashspec"
```

Путь — относительно корневого `.dashspec`. После parse Core **мержит** модуль во вкладку.

### Модуль вкладки — `@tab`

```text
@tab stakeholder

tab stakeholder as "Отчёты заказчика" {
  filter top idle_top as "Строк (TOP)" default 100
}

card stakeholder_peak_by_app as "№1 …" { use lus_stakeholder_peak_by_app }
```

- **`@tab <id>`** обязателен; должен совпадать с `tab <id>` в parent.
- В embedded-режиме: tab-local `filter` в блоке `tab { }` + `card` на верхнем уровне; **shell** (`connector`, `toolbar`, глобальные `filter`) в том же файле **игнорируется** (для dual standalone/embedded одного файла).
- **`@config` в модуле** при merge не используется (берётся parent); в файле допустим для standalone `spec_path`.
- Порядок карточек на вкладке = порядок `card` в модуле.

### Standalone (`spec_path` на модуль)

Файл с `@tab` без `@dashboard` — host собирает одно-вкладочный dashboard:

- разрешены `@config`, `@diagramlibrary`, `@sqldialect`;
- тело после `@tab`: `connector`, `layout`, `filter`, `toolbar`, `card` (как у dashboard, без обёртки `dashboard { }`);
- `id` / `title` runtime = `@tab` id и label из `tab … as "…"` если есть.

### Merge rules

| Сущность | Правило |
|----------|---------|
| `filter` | union; дубликат по `name` → ошибка |
| `card` | union; дубликат по `id` → ошибка |
| `tab.CardIds` | из модуля после merge |
| label вкладки | parent `as "…"`; иначе из модуля |

## Consequences

- `DashSpecParser.Parse(text, specDirectory)` — `specDirectory` обязателен при `dashspec` на tab.
- Product specs (LUS): `docs/dashspec/lus-dev-stakeholder.dashspec` — модуль; soak ссылается через `tab … dashspec`.
