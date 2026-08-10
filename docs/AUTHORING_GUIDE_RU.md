# DashSpec — Authoring Guide

Руководство **автора отчётов**: как устроен продукт, из чего собирается `.dashspec`, как думают фильтры / `bind` / клики, и на что смотреть при dogfood в Host.

| Рядом | Роль |
|-------|------|
| **[HOWTO_RU.md](HOWTO_RU.md)** | Пошаговые рецепты (первый card, catalog, служба) |
| [FILTERS_RU.md](FILTERS_RU.md) | Слои filter → Core → SQL |
| [authoring/README.md](authoring/README.md) | Сгенерированный DSL-справочник + VS Code |
| [`design/`](../design/) | ADR |
| [`samples/demo/`](../samples/demo/) | Эталон |
| [DIAGRAM_KINDS_ROADMAP_RU.md](DIAGRAM_KINDS_ROADMAP_RU.md) | Какие kinds добавлять (world census) |

---

## 1. Модель продукта

```text
  git: .dashcatalog + .dashspec + diagrams/layouts + runtime.toml
                 │
                 ▼
         DashSpec.Host (Blazor Server)
                 │
                 ▼
         SQL Server views / queries
                 │
                 ▼
         браузер: toolbar · cards · clicks
```

1. Ты описываешь отчёт **текстом в git** (не кликами в BI-студии).
2. Host парсит спеку, компилирует SQL из `bind`, рисует UI.
3. Аналитик крутит фильтры и кликает сегменты — поведение задаёшь **ты** (`filter` / `bind` / `on click`).

Core **не знает** конкретную БД продукта. Connector (SqlServer) только выполняет `CompiledQuery`.

Early preview **0.x** — DSL может меняться; ломающее — через ADR.

---

## 2. Файлы и слои документа

| Расширение | Зачем |
|------------|--------|
| `.dashcatalog` | Whitelist отчётов на Host (`entry` → путь к `.dashspec`) |
| `.dashspec` | Отчёт / tab-module: filters, cards, tabs, wiring |
| `.dashdiagram` | Kind + колонки category/value/… |
| `.dashpresentation` | Геометрия chart area |
| `.dashpalette` | Цвета серий |
| `.dashlayout` | ASCII-board карточек или toolbar |
| `*.toml` (runtime) | Connection string + plugin load (deployment, не DSL) |

Слои внутри `.dashspec` ([ADR-0024](../design/DASHSPEC-ADR-0024-document-authoring-layers.md)):

```text
runtime { manifest = "…" }     → какой TOML с connector
configuration { sqldialect, palette }
!include diagrams / presentations / layouts
wiring { use connector, palette, layout grid }
report / body { filters, toolbar, tabs, cards }
```

Host bootstrap смотрит на **catalog**, не на один файл:

```toml
# dash-spec.toml / dash-spec.local.toml
[dashboard]
catalog_path = "…/my.dashcatalog"
```

---

## 3. Каркас экрана (что ты проектируешь)

```text
┌─ catalog dropdown · title · tabs ───────────────────┐
├─ toolbar (filter widgets + chips) ──────────────────┤
├─ card grid (layout board) ──────────────────────────┤
│  card: title · optional local filters · viz · click │
└─────────────────────────────────────────────────────┘
```

| Элемент | Твоя ответственность |
|---------|----------------------|
| **Catalog entry** | Имя в UI + путь к модулю + (опционально) свой runtime |
| **Toolbar** | Какие фильтры видны глобально |
| **Card** | Datasource, diagram, `bind`, `on click`, title |
| **Layout** | Куда встать карточки (`ref` + `[ A B ]`) |
| **Tab module** | Вынести кусок в отдельный `.dashspec` |

Авто-apply + debounce — норма для toolbar chrome; отдельная кнопка «Применить» не обязательна.

---

## 4. Фильтры: три роли

Подробности — [FILTERS_RU.md](FILTERS_RU.md). Кратко для автора:

| Место | Роль |
|-------|------|
| `filter <name> …` | Объявление: date / field / top, колонка, default, label, widget |
| `toolbar …` | Что рисуется на панели |
| `bind a, b` на card | **Что реально попадает в SQL этой карточки** |

### Инвариант

Фильтр на toolbar **сам по себе** карточку не сужает. Без `bind` — только UI-шум.

```text
# Плохо для drill: проекты не слышат user_name
card by_project
  data
    datasource view v_by_project
    bind usage_date
  end data
end card

# Хорошо: после set user_name проекты схлопываются
card by_project
  data
    datasource view v_by_project   # во view должна быть колонка user_name
    bind usage_date, program, location, user_name
  end data
end card
```

### View и колонки

`bind user_name` требует колонку `user_name` **в datasource карточки**.  
Если view — только `(project, usage_date, launch_count)`, Host не из чего строить `WHERE`.

Для category-chart Host после фильтров делает `SUM(measure) GROUP BY category` — дневное зерно + sibling dims нормальны.

### Defaults дат

```text
default = -30d..today
```

Относительные границы резолвятся на сессии; синтаксис — в FILTERS_RU.

---

## 5. Datasource и диаграммы

Предпочтительный путь: **SQL view** + `datasource view …`.

```text
data
  datasource view luf.v_launches_by_form
  bind usage_date, program, location, project, user_name
end data

view
  diagram luf_by_form_bar
end view
```

Альтернативы ([ADR-0018](../design/DASHSPEC-ADR-0018-sql-datasource-carriers.md)): `datasource sql query` / `sql file`.

Diagram (`@diagram`) задаёт kind и привязки колонок (`category` / `value` / оси heatmap). Presentation — «как лежит» chart area; palette — цвета.

Kinds сейчас: line, bar, table, heatmap, pie/donut, … (см. ADR-0003 / samples).

---

## 6. Клики (`on click`)

Whitelist эффектов — [ADR-0028](../design/DASHSPEC-ADR-0028-bounded-card-click-interactions.md).

```text
on click
  set location from x
end click
```

| Эффект | Назначение |
|--------|------------|
| `set <filter> from x\|y\|value` | Drill: значение клика → FilterState |
| `show below list\|kv\|plain data from tooltip\|cell [copy]` | Sticky detail под viz |
| `goto tab <id>` / page / catalog entry | Навигация |
| `invoke` / phrase templates | Плагинные действия (ADR-0034) |

### Поведение Host, которое надо закладывать в UX

1. **Легенда donut/pie** эмитит category-click (фильтр), не Chart.js hide.
2. **Other / Прочие** — свёртка Top-N; **не** реальное значение; Host **игнорирует** клик (иначе пустые карточки).
3. Последовательные клики по разным измерениям: sibling **field**-фильтры заменяются набором текущего клика; **дата** сохраняется. Так меньше пустых AND (`location=/A AND program=B`).
4. Heatmap может за один клик выставить `from x` и `from y`.

Если нужен multi-select по нескольким измерениям сразу — веди пользователя в toolbar, не только в клики.

---

## 7. Вкладки, phases, visibility

- `tab … dashspec "other.dashspec"` — модуль ([ADR-0011](../design/DASHSPEC-ADR-0011-tab-modules.md)).
- `when` / `phase` / `focus` — browse→detail ([ADR-0030](../design/DASHSPEC-ADR-0030-report-scale-pages-gates-and-suites.md)): прятать detail, пока нет выбора.

Типичный drill: heatmap `set` + `goto tab detail` → узкий срез на второй вкладке.

---

## 8. Catalog и несколько runtime

```text
@catalog prod
default soak

entry soak as "Soak"
  dashspec "soak.dashspec"

entry utilities as "Утилиты"
  dashspec "luf/luf-overview.dashspec"
```

Каждый модуль может указать **свой** `runtime.manifest` (другая БД). Host 0.2.1+ резолвит SqlServer CS **на entry**.

Секреты и `[access]` — в host `dash-spec.local.toml`, не в product runtime в git.

---

## 9. Dogfood: как проверять свой отчёт

1. Подними Host (`dotnet run` или служба) → открой URL → при необходимости `/access`.
2. Смени период — все card с `bind` даты должны шевелиться.
3. Кликни одно измерение — чип появился; **зависимые** card сузились.
4. Кликни другое измерение — старый field-чип ушёл (ожидаемо на 0.2.3+).
5. Кликни «Прочие» — фильтр **не** ставится.
6. SQL-контроль: `COUNT(DISTINCT project) … WHERE user_name = …` vs то, что рисует card.

### Частые авторские ошибки

| Симптом | Частая причина |
|---------|----------------|
| После выбора пользователя «все проекты» | Нет `user_name` в `bind` и/или во view |
| «Нет данных» после двух кликов | AND несовместимых измерений; или клик Other |
| Фильтр на toolbar «молчит» | Забыл `bind` на card |
| Parse / load error | Битый путь include / catalog / runtime.toml |
| Карточки считают по-разному | Разное зерно view или разный `bind` — это ок, документируй |

---

## 10. Инструменты автора

| Инструмент | Зачем |
|------------|--------|
| VS Code extension [`editor/vscode-dashspec`](../editor/vscode-dashspec/) | Подсветка, LSP, validate on save |
| `DashSpec.Host validate <path>` | Парс без полного UI |
| `/dev/resolve` (dev) | Разобранная спека |
| `dotnet test DashSpec.slnx` | Регрессии Core/Host |
| DocGen → [authoring/generated/AUTHORING.md](authoring/generated/AUTHORING.md) | Каталог конструкций из кода |

---

## 11. Чеклист нового overview с drill

- [ ] Views содержат все колонки, которыми режешь
- [ ] `filter` объявлены; нужные — в `toolbar`
- [ ] Верхние dim-card: `on click { set … from x }` + bind чужих dim + date
- [ ] Нижние card: bind всех измерений
- [ ] Entry в `.dashcatalog` + рабочий runtime TOML
- [ ] Dogfood: user → projects = SQL-истина, не весь каталог
- [ ] Other не ломает сессию

Пошагово — [HOWTO_RU.md](HOWTO_RU.md).

---

## 12. Карта документов

| Документ | Когда открывать |
|----------|-----------------|
| **Этот Authoring Guide** | Понять модель и контракты автора |
| [HOWTO_RU.md](HOWTO_RU.md) | Сделать конкретный шаг |
| [FILTERS_RU.md](FILTERS_RU.md) | Глубоко про bind/SQL |
| [PLUGINS.md](PLUGINS.md) | Connector / plugin load |
| [authoring/](authoring/README.md) | DSL catalog + editor |
| [`../README.md`](../README.md) | Обзор репо |
