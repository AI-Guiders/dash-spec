# DashSpec — How-to (рецепты для людей)

Практические сценарии: поднять Host, написать первый отчёт, связать фильтры, клики, catalog, несколько runtime, доступ, типичные поломки.

Для зрителя экрана — [USER_GUIDE_RU.md](USER_GUIDE_RU.md).  
Глубина по фильтрам — [FILTERS_RU.md](FILTERS_RU.md).  
Справочник DSL — [authoring/](authoring/README.md) и ADR в `design/`.

---

## Оглавление

1. [Запустить demo Host локально](#1-запустить-demo-host-локально)
2. [Минимальный отчёт: view → diagram → card](#2-минимальный-отчёт-view--diagram--card)
3. [Фильтры: объявить, bind, toolbar](#3-фильтры-объявить-bind-toolbar)
4. [Кросс-фильтры между карточками](#4-кросс-фильтры-между-карточками)
5. [Клик по сегменту → set filter](#5-клик-по-сегменту--set-filter)
6. [Catalog: несколько отчётов на одном Host](#6-catalog-несколько-отчётов-на-одном-host)
7. [Несколько runtime / баз на одном Host](#7-несколько-runtime--баз-на-одном-host)
8. [Доступ по API-ключу](#8-доступ-по-api-ключу)
9. [Layout: сетка карточек и toolbar](#9-layout-сетка-карточек-и-toolbar)
10. [Проверить спеку (validate)](#10-проверить-спеку-validate)
11. [Windows-служба и health](#11-windows-служба-и-health)
12. [Типичные поломки](#12-типичные-поломки)

---

## 1. Запустить demo Host локально

**Нужно:** .NET SDK (см. `TargetFramework` Host, сейчас `net10.0`), SQL Server с demo-схемой (или поправить connection string).

```powershell
git clone https://github.com/AI-Guiders/dash-spec.git
cd dash-spec

# connection string: samples/demo/demo.local.toml (из demo.local.toml.example)
# либо правь samples/demo/demo.toml

dotnet run --project src/DashSpec.Host
```

Открой **http://localhost:5295**.

По умолчанию Host смотрит на catalog demo (`samples/demo/…`). Путь catalog задаётся в `src/DashSpec.Host/dash-spec.toml` / `dash-spec.local.toml`:

```toml
[dashboard]
catalog_path = "…/samples/demo/demo-catalog.dashcatalog"
```

`dash-spec.local.toml` — локальные секреты; **не коммить** connection string с паролем.

---

## 2. Минимальный отчёт: view → diagram → card

### 2.1. View в SQL (рекомендуется)

DashSpec хорошо дружит с **готовыми view**, где уже есть зерно и меры:

```sql
CREATE OR ALTER VIEW dbo.v_launches_by_app
AS
SELECT
  CAST(EventTime AS date) AS usage_date,
  AppName AS app_name,
  COUNT_BIG(*) AS launch_count
FROM dbo.Events
GROUP BY CAST(EventTime AS date), AppName;
```

Правила зерна:

- для period-фильтров держи **`usage_date` (или аналог) в зерне**;
- category-chart Host делает `SUM(measure) GROUP BY category` после фильтров — дневные строки схлопнутся в период;
- если карточку нужно резать по `user_name`, колонка **`user_name` должна быть во view** (иначе bind бесполезен).

### 2.2. Diagram

Файл `diagrams/by-app-bar.dashdiagram`:

```text
@diagram my_by_app_bar

diagram bar
  category = app_name as "Приложение"
  value = launch_count as "Запуски"
  order_by = "launch_count DESC, app_name"
end diagram
```

### 2.3. Card + datasource

```text
card by_app
  title = "Запуски по приложениям"

  data
    datasource view dbo.v_launches_by_app
    bind usage_date
  end data

  view
    diagram my_by_app_bar
  end view
end card
```

Подключи diagram через `!include "diagrams/*.dashdiagram"` в модуле.

Эталон структуры — `samples/demo/`.

---

## 3. Фильтры: объявить, bind, toolbar

Три роли (детали — [FILTERS_RU.md](FILTERS_RU.md)):

| Где | Роль |
|-----|------|
| `filter …` | объявление (date / field / top, колонка, default, label) |
| `toolbar …` | что видно на панели |
| `bind …` на card | что попадает в SQL этой карточки |

Пример:

```text
filters
  filter usage_date
    bind date
      column = usage_date
      default = -30d..today
    end bind
    show
      label = "Период"
    end show
  end filter

  filter app_name
    bind field
      column = dbo.v_events.app_name
    end bind
    show
      label = "Приложение"
      widget = combobox
    end show
  end filter
end filters

toolbar usage_date, app_name

card by_app
  …
  data
    datasource view dbo.v_launches_by_app
    bind usage_date, app_name
  end data
end card
```

Пустой field-фильтр → условие в SQL не добавляется.  
`top` → `SELECT TOP n`, не `WHERE`.

---

## 4. Кросс-фильтры между карточками

Задача: клик/выбор **пользователя** должен сузить **проекты**, **формы**, **часы**.

### Шаг A — колонки во view

Плохо:

```sql
-- только project, usage_date → фильтр user_name применить нельзя
SELECT project, usage_date, COUNT_BIG(*) AS launch_count …
```

Хорошо (как у Form/Hour):

```sql
SELECT
  project,
  location,
  program,
  user_name,
  usage_date,
  COUNT_BIG(*) AS launch_count
FROM …
GROUP BY project, location, program, user_name, usage_date;
```

Category-chart всё равно схлопнет в `SUM(launch_count) GROUP BY project` после `WHERE`.

### Шаг B — bind на карточках

```text
card by_project
  data
    datasource view luf.v_launches_by_project
    bind usage_date, program, location, user_name   -- свой project обычно не bind'ят как обязательный
  end data
end card

card by_user
  data
    datasource view luf.v_launches_by_user
    bind usage_date, program, location, project
  end data
end card

card by_form
  data
    datasource view luf.v_launches_by_form
    bind usage_date, program, location, project, user_name
  end data
end card
```

Правило большого пальца:

- **нижние** детальные карточки — bind всех измерений;
- **верхние** размерности — bind всех **чужих** измерений + дату, чтобы drill работал.

### Шаг C — не путать «нет в базе» и «нет в bind»

Проверка SQL:

```sql
SELECT user_name, COUNT(DISTINCT project) AS projects
FROM luf.v_events
WHERE usage_date >= DATEADD(day, -30, CAST(GETDATE() AS date))
GROUP BY user_name
ORDER BY COUNT_BIG(*) DESC;
```

Если у человека 1–2 проекта, а UI показывает 8 — чини bind/view, не «данные испорчены».

---

## 5. Клик по сегменту → set filter

[ADR-0028](../design/DASHSPEC-ADR-0028-bounded-card-click-interactions.md).

```text
card by_location
  title = "По локациям"

  on click
    set location from x
  end click

  data
    datasource view luf.v_launches_by_location
    bind usage_date, program, project, user_name
  end data

  view
    diagram luf_by_location_donut
  end view
end card
```

| Эффект | Смысл |
|--------|--------|
| `set <filter> from x\|y\|value` | записать значение клика в фильтр |
| `show below list data from tooltip copy` | показать текст под viz |
| `goto tab detail` | сменить вкладку |

Заметки Host (0.2.x):

- клик по **легенде** radial-chart тоже эмитит category-click (не hide dataset);
- сегмент **Other / Прочие** не ставит фильтр;
- последовательные `set` с разных карточек: field-фильтры **заменяются** набором текущего клика (дата сохраняется) — меньше пустых AND-пересечений.

Heatmap drill:

```text
on click
  set usage_date from x
  set user_name from y
  goto tab detail
end click
```

---

## 6. Catalog: несколько отчётов на одном Host

[ADR-0023](../design/DASHSPEC-ADR-0023-dashcatalog.md).

```text
@catalog my_prod
default soak

entry soak as "Soak"
  dashspec "lus-dev-soak.dashspec"

entry stakeholder as "Заказчик"
  dashspec "lus-dev-stakeholder.dashspec"

entry utilities as "Запуск утилит"
  dashspec "luf/luf-overview.dashspec"
```

В host TOML:

```toml
[dashboard]
catalog_path = "D:/…/catalogs/my_prod.dashcatalog"
```

Зритель переключает entry в UI. Новый отчёт = новый `entry` + файл в git.

---

## 7. Несколько runtime / баз на одном Host

Один catalog может ссылаться на отчёты с **разными** `runtime.manifest` (разные connection string).

В модуле:

```text
runtime
  manifest = "luf-runtime.toml"
end runtime
```

Host (с 0.2.1+) резолвит connector **на entry**: SqlServer CS из TOML манифеста entry, а не только из startup singleton.

Практика:

| Файл | Назначение |
|------|------------|
| `lus-runtime.toml` | БД лицензий |
| `luf-runtime.toml` | БД LogUseFunc |
| `dash-spec.local.toml` | catalog_path, `[access]`, порты — не секреты продукта в git |

RO-login для prod-viewers; db_owner только для наката view.

---

## 8. Доступ по API-ключу

В **host** TOML (не в product runtime):

```toml
[access]
api_key = "CHANGE_ME"
```

или env `DASHSPEC_API_KEY`.

| Клиент | Как |
|--------|-----|
| Человек | `/access` → cookie |
| Скрипт | заголовок `X-Api-Key` |
| Закладка | `/?api_key=…` один раз |

Пустой ключ = открытый Host (удобно для локальной разработки).

---

## 9. Layout: сетка карточек и toolbar

Короткий `ref` + ASCII-board ([ADR-0020](../design/DASHSPEC-ADR-0020-card-ref-and-layout-board.md)):

```text
card by_location ref L { … }
card by_project  ref P { … }

tab overview as "Overview"
  layout
    [ L P ]
    [ F   ]
  end layout
end tab
```

Или вынести в `.dashlayout`:

```text
include layout "layouts/overview.dashlayout"
```

Toolbar board — [ADR-0022](../design/DASHSPEC-ADR-0022-toolbar-ref-and-layout-board.md).  
Legacy `toolbar usage_date, app_name` — одна строка, тоже ок.

---

## 10. Проверить спеку (validate)

```powershell
dotnet run --project src/DashSpec.Host -- validate path/to/report.dashspec
```

Или VS Code extension [`editor/vscode-dashspec`](../editor/vscode-dashspec/) (LSP + validate on save).  
Перед F5 extension: `scripts/publish-language-server.ps1`, `npm install` в каталоге extension.

Grammar в редакторе может отставать от парсера — источник правды: Host validate / parse.

Тесты репо:

```powershell
dotnet test DashSpec.slnx
```

---

## 11. Windows-служба и health

Host умеет работать как Windows Service (`Microsoft.Extensions.Hosting.WindowsServices`).

Типичный prod-контур (пример handoff, детали зависят от продукта):

1. Publish Host + connectors + plugins + папка `dashspec/` со спеками.
2. `dash-spec.local.toml` на машине: catalog, access, URL/port.
3. Установка службы → автозапуск.
4. Проверка: `Invoke-RestMethod http://localhost:5295/health`

Логи службы смотри в Event Viewer / stdout redirect, если так настроен installer.

---

## 12. Типичные поломки

### Карточка не реагирует на фильтр

1. Есть ли имя фильтра в `bind` карточки?
2. Есть ли колонка во **view** (не только в `v_events`)?
3. Совпадает ли имя фильтра с `set …` / toolbar?

### После клика «Нет данных»

1. Не кликнул ли **Прочие**?
2. Не осталось ли старых чипов с несовместимым AND? (свежий Host сбрасывает sibling field-фильтры на клике)
3. Проверь SQL тем же пересечением вручную — если строк мало, UI честен.

### «Все проекты у одного пользователя»

Скорее всего `by_project` bind'ит только дату. См. [§4](#4-кросс-фильтры-между-карточками).

### Ошибка загрузки entry / runtime

- битый путь в catalog;
- `runtime.manifest` не найден относительно `.dashspec`;
- connector dll не в output (`plugins` / publish layout);
- SQL login без `SELECT` на view.

### Parse error после правки DSL

Сверься с [ADR-0024](../design/DASHSPEC-ADR-0024-document-authoring-layers.md) (блоки `runtime` / `configuration` / `wiring` / `report`) и живым `samples/demo/`.

---

## Чеклист нового overview с drill

- [ ] View на каждое измерение содержит **все** колонки фильтров, которыми режем
- [ ] `filter` объявлены + в `toolbar`
- [ ] Верхние card: `on click { set <dim> from x }` + bind чужих dim + date
- [ ] Нижние card: bind всех dim
- [ ] Entry в `.dashcatalog`
- [ ] Runtime TOML с рабочим CS
- [ ] Ручной клик: пользователь → проекты = 1…N из SQL, не весь каталог
- [ ] Клик «Прочие» ничего не ломает

---

## См. также

| Документ | Когда |
|----------|--------|
| [USER_GUIDE_RU.md](USER_GUIDE_RU.md) | объяснить экран аналитику |
| [FILTERS_RU.md](FILTERS_RU.md) | слои filter / bind / SQL |
| [PLUGINS.md](PLUGINS.md) | connector / plugin load |
| [authoring/README.md](authoring/README.md) | генерация AUTHORING.md |
| [`design/`](../design/) | ADR |
| [`samples/demo/`](../samples/demo/) | эталон |
