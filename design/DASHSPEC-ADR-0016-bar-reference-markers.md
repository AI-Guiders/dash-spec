# DASHSPEC-ADR-0016: Bar reference markers (purchased seats / entitlement)

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-07-01 |
| **Relates to** | [ADR-0003](DASHSPEC-ADR-0003-diagram-kinds-registry.md), LUS stakeholder report №1 |

## Контекст

Заказчик сравнивает **пик одновременности** по ПО с **закупленным пулом** (`purchased_seats` на уровне `app_group`). На horizontal bar нужен не trend line (динамика во времени), а **per-category limit marker** — у каждого продукта свой лимит.

Metabase: goal / reference line. DashSpec v0.3: только bar без аннотаций.

## Решение

Расширение **`bar`** (extension property, `AllowExtensionProperties`):

```toml
[diagram.lus_stakeholder_peak_by_app_bar]
kind = "bar"
category = "app_name"
value = "peak_concurrent_proxy"
reference = "purchased_seats"
reference_as = "Куплено"
orientation = "horizontal"
```

| Слой | Поведение |
|------|-----------|
| **QueryCompiler** | `reference` попадает в `SELECT` через `DiagramBindings` |
| **CategoryChartPayloadBuilder** | выравнивает `ReferenceValues` по категориям; bar **красный**, если `value > reference` |
| **Host / Chart.js** | custom plugin: **вертикальный штрих** на оси значений в полосе категории; legend + tooltip «Куплено» / «Утилизация %» |
| **Ось значений** | `suggestedMax` = max(peak, reference) × 1.12 |

Отклонено:

- **Combo bar+line dataset** — один глобальный line не работает при разных лимитах по категориям.
- **Новый kind `bullet`** — избыточно до появления второго bullet-сценария.
- **chartjs-plugin-annotation** — лишняя CDN-зависимость; один lightweight plugin достаточен.

## Последствия

- Reference markers только для **category bar** с `reference` в diagram.
- Вертикальный bar (`orientation = vertical`) — markers не рисуются (пока нет кейса).
- Отчёт №4 (utilization %) — отдельный bar без `reference`; % уже в значении.
