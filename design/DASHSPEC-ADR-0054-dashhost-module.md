# DASHSPEC-ADR-0054: `.dashhost` — planet-content shell Host

| | |
|---|---|
| **Status** | Proposed |
| **Date** | 2026-09-25 |
| **Relates to** | [ADR-0021](DASHSPEC-ADR-0021-dashlayout-include.md), [ADR-0023](DASHSPEC-ADR-0023-dashcatalog.md), [ADR-0026](DASHSPEC-ADR-0026-layout-module-scope.md), [ADR-0042](DASHSPEC-ADR-0042-host-control-center-witdb.md), [ADR-0049](DASHSPEC-ADR-0049-git-catalog.md) |

## Context

Host chrome (шапка, подписи, topbar layout, внешние ссылки, язык/TZ/theme) сейчас размазан:

| Слой | Где | Проблема |
|------|-----|----------|
| Название «DashSpec», Help/Settings | `MainLayout.razor` | не planet-content |
| `[presentation]` | `dash-spec.toml` | не в git рядом со спеками |
| `[[links]]` | `*-runtime.toml` | смешано с connection string |
| `[dashboard] catalog_path` | handoff `dash-spec.toml` | дублирует catalog id |
| Ops (api_key, catalog_git) | TOML + WitDB | ок, но смешано с контентом |

Отчёты уже живут в GDL-модулях (`.dashspec`, `.dashlayout`, `.dashcatalog`). Host shell — следующий модуль того же семейства.

## Decision

### 1. Новый файл `.dashhost`

GDL block module, как `@catalog` / `@layout`:

```text
@host sscad_prod

catalog "dashspec/catalogs/sscad-prod.dashcatalog"

configuration
  language = ru
  display_timezone = Europe/Moscow
end configuration

presentation
  product_title = "License Usage"
  catalog_label = "Отчёт"
  color_scheme = dark
  large_field_filter_layout = scroll
end presentation

!include "layouts/host-topbar.dashlayout"

links
  link portal as "Портал"
    url = "https://example/sscad"
    topbar = true
  end link
end links

surfaces
  show help, settings
end surfaces

end host
```

| Блок | Назначение |
|------|------------|
| `catalog "…"` | SSOT путь к `.dashcatalog` (замена `[dashboard] catalog_path` в handoff TOML) |
| `configuration` | язык, TZ (как `[presentation]` сегодня) |
| `presentation` | product title, подписи UI, theme |
| `!include layout` | `scope host` board для topbar / command bar |
| `links` | внешние URL (из `[[links]]` runtime TOML) |
| `surfaces` | какие host routes показывать (help, settings, dev/spec) |

Секреты и connection strings **не** в `.dashhost`.

### 2. `scope host` в `.dashlayout`

Расширение [ADR-0026](DASHSPEC-ADR-0026-layout-module-scope.md):

```text
@layout sscad_topbar
scope host

[ catalog report_picker ]
[ nav ]
[ external_links ]
[ help settings ]
```

Токены — зарезервированные id chrome Host (не card/filter ref отчёта).

### 3. Bootstrap: pointer под `[host]`, не `[dashboard]`

Минимальный ops TOML (или env):

```toml
[host]
dashhost = "dashspec/sscad-prod.dashhost"
database_path = ""   # WitDB, ADR-0042
```

Разрешение `.dashhost` (первый найденный):

1. `[host] dashhost` / `DASHSPEC_DASHHOST`
2. convention: `{contentRoot}/dashspec/{id}.dashhost` при известном deploy id
3. ошибка с подсказкой

Из распарсенного `.dashhost` Host получает `catalog_path`, presentation, links, layout.

**`[dashboard]` в TOML — deprecated:** catalog только из `.dashhost`. Handoff zip без `dash-spec.toml` возможен (только binary + specs + `.dashhost`).

### 4. Merge order (низ → высокий приоритет)

1. `.dashhost` (git / catalog_git) — planet-content SSOT
2. `dash-spec.toml` / `dash-spec.local.toml` — **только ops**: `[host] database_path`, `[access]`, `[catalog_git]`
3. WitDB `host_settings` — live override ops ([ADR-0042](DASHSPEC-ADR-0042-host-control-center-witdb.md))
4. env `DASHSPEC_*` — break-glass

`[presentation]` в TOML — deprecated в favor of `.dashhost`; WitDB может override theme/TZ для оператора.

### 5. Что остаётся в TOML / env / WitDB

| Ключ | Где | Почему не `.dashhost` |
|------|-----|------------------------|
| `api_key` | WitDB / env | секрет |
| `catalog_git` url, password, webhook | WitDB / env | секрет + deploy |
| `database_path` | `[host]` TOML / env | машинный путь |
| connection_string | `*-runtime.toml` + env | секрет (v2: connector ref + env) |
| Kestrel port | `appsettings` | .NET host |

Цель: **git handoff без planet TOML**; ops-слой тонкий или только env.

## Consequences

- SSCAD prod: `sscad-prod.dashhost` + `layouts/host-topbar.dashlayout`; убрать `dash-spec.toml` из zip (оставить `dash-spec.local.toml` на сервере при необходимости).
- `[[links]]` мигрируют из `lus-runtime.toml` в `.dashhost`.
- Parser: `@host`, `scope host`, bootstrap читает catalog из dashhost.
- Studio v2: visual editor для host chrome рядом с catalog.
- Amends [ADR-0023](DASHSPEC-ADR-0023-dashcatalog.md): catalog path может задаваться из `.dashhost`, не только из TOML.

## v1 scope (implementation)

1. Parse `@host` + `catalog` + `presentation` + `links` (subset)
2. `[host] dashhost` в bootstrap; load catalog from dashhost
3. `scope host` layout → `TopbarNav` slot order
4. `product_title`, `catalog_label` вместо хардкода
5. Deprecation warning: `[dashboard] catalog_path`, `[presentation]` in TOML

## Non-goals v1

- WitDB UI для presentation (только ops keys)
- Per-catalog-entry разные host shells (один `.dashhost` на deploy)
- Удаление `dash-spec.toml` loader (backward compat до миграции fleet)

## Open questions

1. Имя ключа: `dashhost` vs `presentation_path` в `[host]`?
2. `surfaces` — whitelist или opt-out (`hide spec_dev`)?
3. Connection strings: оставить `*-runtime.toml` или `connectors` block в `.dashhost` с env substitution?
