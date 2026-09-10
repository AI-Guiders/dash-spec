# DASHSPEC-ADR-0051: Fullscreen detail payload (без Other)

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-09-10 |
| **Relates to** | [ADR-0007](DASHSPEC-ADR-0007-presentation-transform-diagramlibrary.md), [ADR-0002](DASHSPEC-ADR-0002-layout-and-presentation.md), GUIDERS-ADR-0020 (DetailTier) |

## Context

`transform series` сворачивает хвост категорий/серий в агрегат **Other**. Это уместно в компактной карточке, но:

- Other — не измерение (клик/drill ломается);
- fullscreen раньше только увеличивал canvas, **не меняя payload**;
- физически на fullscreen влезает больше категорий — режим «подробнее» должен показывать **полный срез без Other**.

## Decision

### Два слоя данных на card

| View | Payload | Transform |
|------|---------|-----------|
| **Card (Pulse)** | `Chart` / `Matrix` | `transform series` как в spec |
| **Fullscreen (Detail)** | `DetailChart` / `DetailMatrix` | **без** transform (null) |

Host строит `Detail*` только если transform применён (`seriesTransform != null`).

### Host wiring

- `CardRenderResult.ForView(detailView: true)` подменяет Chart/Matrix на Detail*.
- Fullscreen modal: `CardVisualization DetailView=true`.
- Отдельные DOM id (`:detail` suffix) — card и modal coexist без конфликта canvas.
- CSV export: `BuildContext` использует detail payload, если есть.

### Adaptive chrome (GDL DetailTier parity)

- **Horizontal bar:** `height ≈ categories × 26px + 64`, cap 2400px; body scroll.
- **Matrix:** `height ≈ rows × 22px + 96`, cap 2400px.
- **Line / dense:** min height 420–480px в detail.

`presentation.height` остаётся базой для card; detail расширяет, не сужает.

## Non-goals

- Refetch с другим SQL `chart_top` — fullscreen не меняет filter/query.
- Spec keyword `max_expanded` — follow-up при необходимости.
- Deprecate Other в card view — остаётся opt-in через transform.

## Consequences

- Карточка компактна (TOP-N + Other где задано).
- Expand = все категории из уже загруженных rows, кликабельные, с catalog colors.
- Other остаётся только в card view, не в detail/export.
