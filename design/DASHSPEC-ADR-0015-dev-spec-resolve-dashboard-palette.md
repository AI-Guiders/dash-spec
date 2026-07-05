# DASHSPEC-ADR-0015: Dev spec resolve, dashboard palette

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-06-30 |
| **Relates to** | [ADR-0014](DASHSPEC-ADR-0014-chart-series-colors.md), [ADR-0011](DASHSPEC-ADR-0011-tab-modules.md) |

## Context

Разбиение spec на `.dashspec` + `@diagramlibrary` улучшает DRY, но ухудшает **authoring UX**: `use lus_dau_bar` не показывает effective diagram/presentation без просмотра TOML. Runtime UX (Host) не страдает — merge уже есть в Core (`SpecResolver`).

## Decision

### Dashboard-level palette

```text
dashboard "License Usage — Dev Soak" {
  palette lus_apps
  …
}
```

| Слой | Сила |
|------|------|
| `dashboard.palette` | базовый `[palette.*]` для всех chart-карточек |
| `diagram.color_palette` | override |
| `presentation` | override |

Цвета серий: `ChartColorResolver` + stable hash по имени (ADR-0014).

### Dev resolve API (Host, Development only)

| Endpoint | Ответ |
|----------|--------|
| `GET /dev/resolve` | JSON `ResolvedSpecExport` — все карточки после merge |
| `GET /dev/resolve/card/{id}` | одна карточка |

Источник — **файл на диске** (`dash-spec.local.toml` → `spec_path`), не session state.

Core: `SpecResolveExporter.Export(document, library)`.

### Spec inspector UI

- страница **`/dev/spec`** — accordion карточек, diagram / presentation / transform / effective palette
- навигация **Dashboard | Spec** в topbar (только Development)

### Auto-reload

`DevSpecFileWatcherService` следит за каталогом root `.dashspec` (`.dashspec`, `.toml`) → `DevSpecReloadNotifier` → dashboard session reload без рестарта Host.

## Non-goals (v1)

- VS Code extension / LSP
- resolve uploaded spec (только configured path)
- prod `/dev/*` endpoints

## Consequences

- LUS: `palette lus_apps` в `lus-dev-soak.dashspec`; `color_palette` убран из diagram presets library
- Authoring: правка TOML → save → dashboard + `/dev/spec` обновляются
- CLI `dash-spec resolve` — отдельный шаг (reuse Core exporter)
