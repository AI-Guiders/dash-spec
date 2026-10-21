# DASHSPEC-ADR-0110: Оболочка хоста как слотовая плоскость — nav-плагин и открытые контент-слоты

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-10-21 |
| **Relates to** | [ADR-0072](DASHSPEC-ADR-0072-unified-layout-slot-plane.md) (плоскость слотов: host grid = card grid), [ADR-0061](DASHSPEC-ADR-0061-layout-board-weights-and-filter-place.md) (веса и place), [ADR-0063](DASHSPEC-ADR-0063-layout-board-nest.md) (nest), [ADR-0073](DASHSPEC-ADR-0073-author-navigation-surface.md) (навигационная поверхность), [ADR-0074](DASHSPEC-ADR-0074-host-shell-composed-view.md) (host как оболочка; композер — предшественник), [ADR-0054](DASHSPEC-ADR-0054-dashhost-module.md), [ADR-0059](DASHSPEC-ADR-0059-vertical-viz-plugins.md) / [ADR-0060](DASHSPEC-ADR-0060-vertical-filter-plugins.md) (вертикальные плагины, паттерн), [ADR-0101](DASHSPEC-ADR-0101-single-unit-file-type.md) (единый `.ds`) |

## Context

Host-layout со `scope host` ([ADR-0054]) уже декларирует слоты оболочки (`[ catalog ] [ nav ] [ external_links ] [ help settings spec_dev ]`), но `HostTopbarLayoutResolver` разворачивает board в плоский список для одной строки topbar'а — геометрия, заявленная в спеке, теряется. Полосы навигации внутри отчёта (вкладки/листы — `DashboardTabBar`, `DashboardPagePicker`) рендерятся в теле страницы отдельно и с эвристиками `Count > 1` — первая строка таблицы противоречий [ADR-0074].

Запрос оператора: выбор отчётов и навигация по отчёту — полноценное меню слева («взрослый BI»); навигационный рендер — плагин с первого дня; хост остаётся «тупым»: он даёт раскладку и слоты. Ранняя версия этого ADR вводила «зоны» — отдельный механизм для навигации; отзывается: **оболочка уже даёт грид, в этот грид можно размещать что угодно** — зоны дублировали бы плоскость слотов.

Предшественник: полный композитор — [ADR-0111] (`VisualModel` / `VisualCompositor`; низкоуровневая сверка с устаревшими допущениями [ADR-0074] зафиксирована там). Этот ADR не вводит собственного поставщика навигационных данных — все рендеры потребляют `VisualModel`.

## Decision

### 1. Оболочка — новый scope единой плоскости слотов ([ADR-0072])

| Понятие | Значение для оболочки |
|---------|----------------------|
| Scope | `HostShell` (рядом с `HostTabBoard`, `HostPageToolbar`) |
| Доска | Существующий host-layout со `scope host` — теперь **вся** оболочка, а не только topbar |
| Слот | Ячейка board: словарь слотов (`catalog`, `nav`, `external_links`, `help`, `settings`, `spec_dev`) + новые контент-kind'ы (§2) |
| Placement | Те же веса ([ADR-0061]) и nest-доски ([ADR-0063]) — **грамматика не меняется** |
| Рендер | Dispatch по `(scope, content kind)` через реестр рендеров — плагины и built-ins ([ADR-0059]/[ADR-0060] паттерн) |

Инвариант [ADR-0072] сохранён: Host итерирует `LayoutSlotDescriptor` по placement; dispatch — по реестру, без ad-hoc Razor-веток.

### 2. Новые контент-kind'ы — два, оба не о навигации

- `content` — тело отчёта (сегодня — неявное «всё под topbar'ом»). Слот `content` в доске = тело рисуется в этой ячейке.
- `brand` — блок заголовка оболочки (сегодня — зашитая левая часть topbar'а).

Набор контент-kind'ов оболочки — **открытый**: в грид кладётся что угодно (навигация, тело, завтра — вертикальные фильтр-виджеты [ADR-0060], KPI-полоса, текстовая панель) тем же механизмом, без правки ядра.

### 3. Меню слева = обычная доска оболочки

```
scope host

[ shell_side:1 shell_main:3 ]

nest shell_side
  [ brand ]
  [ catalog ]
  [ nav ]
end nest

nest shell_main
  [ external_links help settings spec_dev ]
  [ content ]
end nest
```

Сайдбар — не концепция, а узкая колонка с весом `:1`, в которой слоты складываются вертикально. Никаких «зон»: место навигации определяется placement'ом слота.

### 4. Слот `nav` — плагин с первого дня

- Данные навигации — **`VisualModel.NavigationBands`** композитора ([ADR-0111]; нормативный минимум [ADR-0074] §2: `BandKind = portal | section | page`, `ShowMode`, `Items[]`); активный маршрут и busy — сессия, не композитор.
- Рендер — **`plugins/nav/DashSpec.Plugin.Nav.Tree`** (RCL, образец — `DashSpec.Plugin.Viz.Builtins`): один `IDashSpecPlugin`, дескриптор `nav_tree`, компонент дерева «портал → разделы → листы»; сервисы оболочки — инъекцией, знаний о `DashboardPageController` и внутренних типах страницы — ноль.
- Реестр рендеров слотов оболочки — по образцу `FilterWidgetRegistry` ([ADR-0035]): built-in рендеры (`catalog_dropdown`, топбарные ссылки, `content`, `brand`) + подключаемые дескрипторы плагинов; подмена вида слота — декларация.

### 5. Прямая миграция (ломающая; авторы спек — только мы)

| Удаляется | Взамен |
|-----------|--------|
| `HostTopbarLayoutResolver` и плоский путь topbar-слотов | Резолвер грамматики оболочки |
| Зашитые `<header class="topbar">` + `<TopbarNav>` в `MainLayout` | Грид оболочки из доски + реестр рендеров |
| `DashboardTabBar` / `DashboardPagePicker` в теле `Home.razor` (gate `Count > 1`) | Полосы рисует рендер слота `nav` — единственное место навигации |
| Gate `Entries.Count > 1` в `DashboardCatalogPicker` | Дерево `nav_tree` рисует то, что объявлено; прятать — решение автора доски |
| `samples/demo` host-части (`demo.ds`, `layouts/host-topbar.ds`) | Доска из §3 (`layouts/host-shell.ds`) |

Host-модуль без shell-доски со слотом `content` — **ошибка компиляции** с читаемым сообщением и ссылкой на образец; тихих fallback'ов нет («явность вместо магии», [ADR-0074]).

## Rejected

| Идея | Почему |
|------|--------|
| «Зоны» (ранняя версия этого ADR) | Параллельный механизм рядом с уже существующей плоскостью слотов — второй способ думать об одном |
| `presentation { portal = sidebar }` в host-модуле | Геометрия — не «оформление»; место ей — доска |
| Сайдбар, зашитый в `MainLayout` | Хост перестаёт быть тупым; каждый новый вид навигации — правка ядра |
| Сайдбар как widget | Widget — контрол ([ADR-0073]); навигация — полоса, не контрол |
| Proto-композер / `INavBandsSource` как переходная прослойка | Одноразовый код; композитор ([ADR-0111]) реализуется первым — рендеры сразу потребляют `VisualModel` |

## Implementation phases

| Phase | Deliverable |
|-------|-------------|
| **P0** | Композитор [ADR-0111]: `VisualModel` (NavigationBands, InitialRoute, Chrome/Toolbars, LayoutPlanes, Shell), JSON-артефакт, тесты без Blazor |
| **P1** | Scope `HostShell`: парсер принимает `content`/`brand` в host-доске, lint «нет content-слота», резолвер placement'ов; тесты парсера/резолвера |
| **P2** | Реестр рендеров оболочки; RCL-плагин `DashSpec.Plugin.Nav.Tree`; `MainLayout` = грид из доски; удаление legacy-пути (§5); demo на новую доску |
| **P3** | Токены карточек (чёткие границы, KPI-акцент) — вне этого ADR, правка `tokens.css`/`cards.css` |

## Consequences

- «Хочу меню слева» = доска в спеке; новый вид навигации = плагин; хост не растёт ни на одну догадку.
- Открытые контент-kind'ы оболочки — готовый путь для вертикальных фильтров ([ADR-0060]) и будущих блоков оболочки.
- [ADR-0074] завершается целиком: Host перестаёт быть «вторым автором» и в навигации, и в дефолтах.
