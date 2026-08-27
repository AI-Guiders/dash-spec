# DASHSPEC-ADR-0042: Host Control Center + WitDB settings SSOT

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-08-27 |
| **Relates to** | [ADR-0025](DASHSPEC-ADR-0025-git-catalog.md), [ADR-0041](DASHSPEC-ADR-0041-git-catalog-push-sync.md), Forge [ADR-0057](https://github.com/AI-Guiders/agent-forge/blob/main/design/FORGE-ADR-0057-instance-settings-and-control-center.md) |

## Context

Оператор устал править `dash-spec.local.toml` / env для access key, `catalog_git`, sync webhook secret. Нужен **Host Control Center** в экосистеме DashSpec (не клон Forge users/MFA/orgs).

**Не путать с LUS API:** LUS держит runtime settings в **SQL Server** (`lus.api_settings`). Forge instance settings — в **WitDB** (`OutWit.Database.EntityFramework`). Host — одиночный Windows service без SQL Server dependency для своей конфигурации → **WitDB**, как Forge.

## Decision

### 1. Storage

| | |
|---|---|
| Engine | **WitDB** via `OutWit.Database.EntityFramework` **14.0.1** (align Forge / CDP) |
| File | `%ProgramData%\DashSpec\host-settings.witdb` (override: `[host] database_path` / `DASHSPEC_HOST_DB`) |
| Table | `host_settings` — `(section, key)` PK, `value` text, `updated_at`, `updated_by` |

Sections v1:

| Section | Keys (examples) |
|---------|-----------------|
| `access` | `api_key` |
| `catalog_git` | `enabled`, `url`, `branch`, `path`, `pull_interval_minutes`, `username`, `password`, `sync_webhook_secret`, `sync_repo_slug`, `sync_allow_unsigned`, `cache_directory` |

Secrets: UI write-only (empty field = keep existing). Never echo plaintext password/secret in GET forms.

### 2. Merge order (low → high)

1. `dash-spec.toml` (+ `dev` / `local` overlays) — bootstrap / disaster recovery  
2. **WitDB `host_settings`** — live SSOT after first Control Center save  
3. **Env** — break-glass (`DASHSPEC_*`) wins over WitDB  

Export: Control Center «Download TOML fragment» for air-gap backup (non-goal: auto-write `local.toml`).

### 3. Control Center UI (Host Blazor)

Route: `/admin` → redirect `/admin/access`; content at `/admin/{section}`.

**Chrome:** Forge/GH settings shape — **List | Data** (left section nav, right one-section pane). Not a single scrolling form.

| Section id | Purpose |
|------------|---------|
| `access` | set/rotate API key |
| `catalog` | git url/branch/path/interval; **Sync now**; last sync status |
| `sync` | inbound URL; **Generate secret**; Forge `FORGE_DASHSPEC_WEBHOOK_*` |
| `export` | redacted TOML fragment (air-gap backup) |

Auth: same Host access gate (`[access]` / cookie). No anonymous admin.

Ops (not settings): Sync now → existing `GitCatalogSyncService` (ADR-0041).

### 4. Non-goals v1

- Multi-tenant / multi-user roles beyond single access key  
- Editing SQL connector connection strings in UI (stay toml/env until needed)  
- Pairing protocol with Forge (paste secret manually)  
- Replacing git catalog with WitDB-stored specs  

### 5. Consequences

- Operator configures Host from browser after first install.  
- Same WitDB stack familiarity as Forge.  
- Bootstrap toml still required for cold start / DB path / connectors.  

## Alternatives considered

| Alternative | Why not |
|-------------|---------|
| SQL Server like LUS API | Extra dependency for Host; overkill for key/value settings |
| Edit `local.toml` from UI | File locks, Windows service permissions, no audit |
| Plain SQLite via Microsoft.Data.Sqlite | Possible, but WitDB already standard in Guiders stack |
| Settings only in Forge | Host must stay generic-git; secrets belong on Host |

## Implementation sketch

1. `DashSpecHostDbContext` + `EnsureCreated` / migrate `host_settings`.  
2. `HostSettingsService` — get/merge/upsert/export.  
3. Apply WitDB overlay onto `DashSpecTomlRoot` after toml load (before sync service).  
4. Blazor `/admin` pages.  
5. Tests: merge order + secret write-only.
