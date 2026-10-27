# DASHSPEC-ADR-0115: Radzen-рендер — паритет фич с self-made

| | |
|---|---|
| **Status** | Accepted · Implemented |
| **Date** | 2026-10-27 |
| **Relates to** | [ADR-0112](DASHSPEC-ADR-0112-render-stack-and-theme-token.md), [ADR-0111](DASHSPEC-ADR-0111-assembled-slot-plane.md), [ADR-0079](DASHSPEC-ADR-0079-dashflow-type-system.md), [ADR-0077](DASHSPEC-ADR-0077-host-ui-copy-ssot.md) |

## Decision

- **Radzen-рендер покрывает фичи self-made** (копирование колонок таблицы, экспорт и т.п.) — и вправе давать больше.
- Таблица: сортировка/фильтры/пейджинг/ресайз (уже были) + **column picking и reorder** (интерактив зрителя, без спек-поверхности); **copy-per-column** — кнопка в шапке колонки, копирует отфильтрованную колонку целиком (тот же файл, что builtin).
- **Экспорт остаётся общим chrome-плагином `card_export`** (payload-based, все семьи карточек) — встроенный экспорт RadzenDataGrid не используется: он только у грида и пишет не то (display-строки вместо wire-ячеек, ADR-0079).
- **Заморозка колонок — ключ спеки**: `presentation frozen_columns = N` (намерение автора; self-made игнорирует).
- **Локализация строк Radzen — плагинный `ILocalizer`**: ru-спутника у Radzen нет; плагин отдаёт переводы ключей `RadzenStrings` для ru-культуры, остальные — дефолт Radzen.

## Реализация

`RadzenCardViz.razor` (атрибуты грида, HeaderTemplate-глиф, Frozen), `table-copy.js` + `RadzenVizScripts` (монтирование в App.razor под Radzen-стек), `RadzenRuLocalizer` + `RadzenRuStrings.resx`, `TablePresentation.FrozenColumns`.
