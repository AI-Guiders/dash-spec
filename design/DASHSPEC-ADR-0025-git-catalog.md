# DASHSPEC-ADR-0025: Git catalog source

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-08-17 |
| **Relates to** | [ADR-0023](DASHSPEC-ADR-0023-dashcatalog.md) |

## Context

Specs и `.dashcatalog` могут жить в git, не только в папке рядом со службой. URL репозитория и учётки — **на стороне оператора** (Forge / git server), не в dash-spec repo.

## Decision

Оператор задаёт в `dash-spec.local.toml` на сервере Host (шаблон):

```toml
[catalog_git]
enabled = true
url = "http://<host>:<port>/git/<org>/<repo>.git"
branch = "main"
path = "catalogs/<name>.dashcatalog"
pull_interval_minutes = 15
username = "<user>"
password = "<secret>"
```

Env: `DASHSPEC_CATALOG_GIT_URL`, `DASHSPEC_CATALOG_GIT_BRANCH`, `DASHSPEC_CATALOG_GIT_PATH`, `DASHSPEC_CATALOG_GIT_USERNAME`, `DASHSPEC_CATALOG_GIT_PASSWORD`, `DASHSPEC_CATALOG_GIT_PULL_MINUTES`.

При `enabled = true` Host:

1. `git clone` / `git fetch + reset` в `%ProgramData%\DashSpec\git-catalogs\<hash>`
2. Устанавливает `dashboard.catalog_path` на файл внутри кэша
3. `GitCatalogSyncBackgroundService` — периодический pull + reload UI при изменении catalog

`catalog_path` остаётся fallback (dev без git).

## Non-goals

- Хранение prod URL/credentials в dash-spec git
- Git credential manager UI
- Per-entry permissions
