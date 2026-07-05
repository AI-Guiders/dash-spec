# DASHSPEC-ADR-0019: `@runtime` — manifest вне DSL

| | |
|---|---|
| **Status** | Accepted · v0.6 |
| **Date** | 2026-07-02 |
| **Supersedes** | [ADR-0001](DASHSPEC-ADR-0001-connectors-as-plugins.md) § «`@config`» (имя) |
| **Relates to** | [ADR-0017](DASHSPEC-ADR-0017-file-includes-and-stdlib.md), [ADR-0024](DASHSPEC-ADR-0024-document-authoring-layers.md) (блочный `runtime { manifest = … }` — канон surface) |

## Context

Преамбула `.dashspec` смешивала **язык отчёта** (dashboard, cards, `include diagram`) и **deployment** (connection string, plugin DLL). Директива `@config` звучала как часть DSL, хотя это runtime manifest в TOML.

PlantUML-подобный authoring: `.dashspec` + файловые include — без TOML presets. Остаётся один внешний файл для Host: connectors/plugins.

## Decision

### `@runtime` → `runtime { manifest = … }`

```text
runtime {
  manifest = "demo.toml"
}
```

| Ключ | Назначение |
|------|------------|
| `manifest` | **обязателен** в `runtime { }` — путь к TOML (относительно `.dashspec`): `[connectors.*]`, `[plugins]`, `[[plugins.load]]` |

Surface `@runtime "…"` и `@config "…"` **удалены** — [ADR-0024](DASHSPEC-ADR-0024-document-authoring-layers.md).

Содержимое manifest **не** DashSpec DSL — только infra. Секреты: `*.local.toml`, env `Connectors__*`.

### Host bootstrap (без изменений по смыслу)

`dash-spec.toml` у Host — только `[dashboard] spec_path` (или `DASHSPEC_SPEC_PATH`). Не смешивать с `@runtime` продукта.

### Demo sample

`samples/demo/` — без `@diagramlibrary`: `diagrams/`, `presentations/`, `palettes/`, явные `datasource`/`bind` на card ([ADR-0017](DASHSPEC-ADR-0017-file-includes-and-stdlib.md)).

## Non-goals v0.6

- Замена TOML manifest на JSON/YAML/env-only (отдельный инкремент)
- Hot reload `@runtime` / смена manifest без рестарта Host

## Consequences

- Core: `ReadRuntimePath`, `ConsumedRuntimePath`; `@config` → alias
- LUS + demo: `@runtime "…local.toml"`
- Tomlyn в Host — только bootstrap + runtime manifest; `SpecLibrary` TOML — deprecated path для старых spec
