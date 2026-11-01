---
type: ADR
id: DASHSPEC-ADR-0117
title: "dsc — CLI composition root; компилятор — библиотека за контрактом"
status: "Proposed"
date: 2026-11-01
tags: [dashspec, adr, cli, composition]
language: ru
relates:
  - DASHSPEC-ADR-0033
  - DASHSPEC-ADR-0047
  - DASHSPEC-ADR-0074
  - DASHSPEC-ADR-0097
  - DASHSPEC-ADR-0101
  - DASHSPEC-ADR-0102
  - DASHSPEC-ADR-0103
---

# DASHSPEC-ADR-0117: dsc — CLI composition root; компилятор — библиотека за контрактом

## Context

- Host доведён до микроядра-композитора (ADR-0033, ADR-0074, ADR-0110/0111): viz/filter/stack/surfaces — плагины, Host всё меньше владеет, всё больше собирает.
- Компилятор (`DashSpec.Execution.Compilation` за `DashSpec.Compiler.Contract`) — библиотека, живущая в трёх процессах: Host, LanguageServer, DocGen (+ тесты). Кэш корпусов — статический, per-process (`DashSpecCompilerServicesHost`).
- CLI-поведение уже написано, но заперто в `Program.cs` Host (ветка `validate <файл>`) и поднимает только builtins: `DashSpecBuiltinContributorRegistrar` — «without external assemblies (CLI validate, Host startup)». LanguageServer тоже не поднимает манифест-каталог (ссылается только на Execution.Compilation/Runtime). Итог: CLI-валидация и рантайм видят **разный словарь** — спека с extension-блоком внешнего плагина проверяется не тем языком, которым живёт хост.
- Первый кусок композиции уже вынесен из Host в проект `DashSpec.Viewer`, но это смесь трёх ролей: (а) композиция — реестры (`DashSpecContributorRegistry`, viz/filter-widget/toolbar), `ViewerPluginBootstrap`, builtins-регистратор, `DashSpecParseOptionsProvider`, `ViewerPluginHost` (порт `IViewerPluginHost` — в `DashSpec.Abstractions`); (б) UI-биндинг — `CardVizComponentRegistry`/`FilterWidgetComponentRegistry` регистрируют `IComponent`-типы (порт `ICardVizComponentResolver` — в `DashSpec.Viz`; отсюда `FrameworkReference: AspNetCore.App` в csproj); (в) session-runtime — `DashSpecActionDispatcher` с `IJSRuntime`, его единственный внешний потребитель — `Surface.Blazor/DashboardPageController`. Имя — артефакт эры «Web Host = viewer» (ADR-0047).
- В конфедерации механизм плагин-хостинга уже стандартизован отдельной гиперлинией [`guiders-plugin-host`](https://github.com/AI-Guiders/guiders-plugin-host) (`AIGuiders.PluginHost.*`: manifest schema, collectible ALC, assembly resolver, layout-таргеты; reference consumer — agent-forge, GUIDERS-VISION-0001). dash-spec его пока не ссылает: `DashSpecPluginLoader` в Host — ручная реализация того же класса задач.

## Decision

1. **`dsc` — тонкий exe, второй composition root. Не плагин.** Компилятор — ядро языка, не точка расширения: плагинный ABI (`DashSpec.Abstractions.Plugins`) лежит ниже `Compiler.Contract`, а плагины — это словарь (parse options: `ExtensionBlockKeywords`, `KnownClickSources`, `KnownActionHandlers`…), который ест компилятор. Направление зависимостей: плагины → registry → parse options → компилятор. Плагонизировать надо то, у чего есть вариативность; реализация языка одна, и Host с LanguageServer хотят именно её.
2. **`DashSpec.Viewer` расщепляется по ролям; композиция получает чистый дом — `DashSpec.Composition`.** В ядро идут реестры семейств, `ViewerPluginBootstrap`, builtins, `DashSpecParseOptionsProvider`, plugin-host — без `AspNetCore`-зависимости. UI-биндинг (`IComponent`-реестры) и session-runtime (`DashSpecActionDispatcher`) уходят в `DashSpec.Surface.Blazor` — там их потребители; проект `DashSpec.Viewer` ликвидируется. В `DashSpec.Composition` же переезжают из `DashSpec.Host/Plugins` `DashSpecPluginLoader` и TOML-манифест (`dash-spec.toml`): ядро владеет политикой и доменом (bundles/tiers/providers, словарь, builtin-dedup). **Механизм** (manifest schema, ALC-политика, layout пакета) в конфедерации уже вынесен в `AIGuiders.PluginHost.*` — целевое состояние: `DashSpec.Composition` сидит на нём вместо собственного лоадера; миграция — отдельным ADR после `dsc`. Ядро — шов, который переживёт эту смену незаметно для корней: Host и `dsc` — равные корни над `DashSpec.Composition`; Host перестаёт владеть механизмом.
3. **Паритет словаря — обязательное свойство корней.** `dsc` собирает тот же манифест и тот же каталог плагинов, что и web-Host. Ветка `validate` переезжает из `Program.cs` Host в `dsc` (в Host — подсказка-редирект на один релиз).
4. **Out of scope — out-of-process компилятор-демон.** Боль (изоляция крашей/памяти) не зафиксирована, а граница требует сериализации F# IR (против ADR-0103) и добавляет латентность в редакторские сценарии. Пересмотреть — по факту боли, не раньше.

## Реализация

1. Расщепление `DashSpec.Viewer` (отдельный PR до всего остального, поведение не меняется): новый `DashSpec.Composition` (без `FrameworkReference`) — реестры, bootstrap, builtins, parse options provider, plugin-host; `CardVizComponentRegistry` + `FilterWidgetComponentRegistry` + `DashSpecActionDispatcher` → `DashSpec.Surface.Blazor`; проект Viewer удаляется из решения.
2. Перенос `DashSpecPluginLoader` и TOML-конфига из `DashSpec.Host/Plugins/` в `DashSpec.Composition` — к builtins registrar и parse options provider. Host переключается на ссылку. Политика читается каноническим стеком: `TomlConfigurationProvider` (собственный `IConfigurationProvider` над Tomlyn) + слои `dash-spec.toml → .dev → .local → env` через `AddTomlFile`, manifest строится из `IOptions<DashSpecPluginsPolicy>` — вместо текущего моста «typed root → `Flatten()` → `AddInMemoryCollection`». Оба корня получают идентичный конфиг-стек одним вызовом. Доменные каталоги (команд, корпус `.ds`) — не через Configuration: там типизированные парсеры (CommandSources-паттерн).
3. `src/DashSpec.Cli` (AssemblyName `dsc`) — миграция ветки `validate`: вход — `.ds`, классификация content-first по module-корню (ADR-0101); легаси-расширения (`.dashcatalog`, `.dashhost` и др.) — compat-чтение, не первый класс. Далее `dsc check <dir>` — корпус целиком, diagnostics в stdout/JSON, exit code для CI.
4. `Program.cs` Host: `validate` → подсказка «moved to dsc»; словарь поднимается через `DashSpec.Composition` — закрывает builtins-only validate.
5. LanguageServer: подключение `DashSpec.Composition` для plugin-aware словаря — отдельным шагом, не блокирует `dsc`.
6. Миграция механизма загрузки на `AIGuiders.PluginHost` — параллельной веткой, не блокирует шаги 1–5 (шов `DashSpec.Composition` прячет её от корней):
   - **механизм**: `LoadFromAssemblyPath(Default ALC)` + shared-assembly resolution → `AIGuiders.PluginHost.Runtime` (pins + collectible ALC — бонусом выгрузка, которой сейчас нет); layout-таргеты `AIGuiders.PluginHost.Build` вместо `StageConnectorPlugins`;
   - **политика остаётся дома**: bundles/tiers/providers — `dash-spec.toml`; манифест-схема PluginHost — про ALC/порядок/пакеты, не про активацию. `host-runtime.manifest.json` — производный, генерируется. Расширение гиперлинии политиками — только по запросу второго потребителя;
   - **поверхность миграции мала**: динамически грузятся 4 DLL (Export, ViewSwitch, SqlServer, Postgres) — viz/filter/stack статические ссылки;
   - **гейты слияния**: Host.E2E + conformance (`AIGuiders.PluginHost.Conformance`) + smoke 4 внешних плагинов.

## Rejected

- **Компилятор как плагин** — движок становится peer'ом собственных точек расширения (его пришлось бы грузить после всего словаря); выгоды нет, вариативности нет.
- **Out-of-proc демон сейчас** — см. Decision 4; граница ради границы.
- **Оставить `validate` в Host** — CI-проверка тянет web-хост бинарник, а словарь расходится с рантаймом (расхождение зафиксировано док-комментарием `RegisterBuiltins`).
- **Rename всего `DashSpec.Viewer` → `DashSpec.Composition`** — проект не только композиция: `IComponent`-реестры (UI-биндинг) и `IJSRuntime`-диспетчер (session-runtime) не композиционные; переименование закрепило бы смесь под честным именем.
- **Новый проект рядом с уже существующим ядром** — ядро есть внутри Viewer; плодить дубль — хуже, чем расщепить существующий.
- **Имя `DashSpec.PluginHost` для ядра** — в конфедерации `AIGuiders.PluginHost.*` означает механизм (ALC/manifest/layout); наша сущность — политика и домен. Тень-совпадение имён разной семантики — ложь похуже «Viewer».
- **Оставить имя `Viewer` ядру композиции** — имя врёт: ни одного визуального компонента там нет. Появление второго composition root (dsc), которому «Viewer» не нужен вовсе, делает эту ложь слышимой.
