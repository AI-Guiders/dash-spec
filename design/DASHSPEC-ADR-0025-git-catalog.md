# DASHSPEC-ADR-0025: Git catalog source

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-08-17 |
| **Relates to** | [ADR-0023](DASHSPEC-ADR-0023-dashcatalog.md) |

## Context

Specs и `.dashcatalog` могут жить в git, не только в папке рядом со службой. Host вызывает `git clone` / `git fetch` — **любой** remote, который понимает git CLI (GitHub, GitLab, Forge, Gitea, Azure DevOps, внутренний git HTTP). URL и учётки — на стороне оператора, не в dash-spec repo.

## Decision

Оператор задаёт в `dash-spec.local.toml` на сервере Host (шаблон):

```toml
[catalog_git]
enabled = true
url = "https://<git-host>/<org>/<repo>.git"
branch = "main"
path = "catalogs/<name>.dashcatalog"
pull_interval_minutes = 15
username = "<user>"
password = "<secret>"
```

Env: `DASHSPEC_CATALOG_GIT_URL`, `DASHSPEC_CATALOG_GIT_BRANCH`, `DASHSPEC_CATALOG_GIT_PATH`, `DASHSPEC_CATALOG_GIT_USERNAME`, `DASHSPEC_CATALOG_GIT_PASSWORD`, `DASHSPEC_CATALOG_GIT_PULL_MINUTES`.

При `enabled = true` Host:

1. **Cold start** — использует `[dashboard] catalog_path` (вшитый fallback / bootstrap). Git **не блокирует** подъём службы.
2. **Первый sync** — сразу после старта (`GitCatalogSyncBackgroundService`) + **Sync now** в Control Center + webhook (ADR-0041).
3. `git clone` / `git fetch + reset` в `%ProgramData%\DashSpec\git-catalogs\<hash>` при успешном sync
4. `catalogState` hot-reload при изменении `.dashcatalog`
5. `GitCatalogSyncBackgroundService` — периодический pull + reload UI при изменении catalog

`catalog_path` остаётся fallback (dev без git).

## Non-goals

- Хранение prod URL/credentials в dash-spec git
- Git credential manager UI
- Per-entry permissions
- Поддержка только одного vendor (Forge) — URL generic

## See also

- Push-triggered sync (webhook) + редкий poll как safety net: [ADR-0041](DASHSPEC-ADR-0041-git-catalog-push-sync.md)
