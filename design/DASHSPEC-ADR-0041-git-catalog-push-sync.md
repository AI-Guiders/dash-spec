# DASHSPEC-ADR-0041: Git catalog sync on push (webhook), poll as safety net

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-08-27 |
| **Relates to** | [ADR-0025](DASHSPEC-ADR-0025-git-catalog.md), Forge [ADR-0005](https://github.com/AI-Guiders/agent-forge/blob/main/design/FORGE-ADR-0005-ci-webhook-bridge.md), Forge [ADR-0067](../../../../agent-forge/design/FORGE-ADR-0067-outbound-push-fanout.md) (sibling repo) |

## Context

[ADR-0025](DASHSPEC-ADR-0025-git-catalog.md) задаёт `GitCatalogSyncBackgroundService` с **`pull_interval_minutes = 15`**. Для каталога отчётов это перебор: большинство тиков — no-op, а после push оператор всё равно ждёт до четверти часа.

Forge уже эмитит outbound `push` (post-receive → CI webhook, [FORGE-ADR-0005](https://github.com/AI-Guiders/agent-forge/blob/main/design/FORGE-ADR-0005-ci-webhook-bridge.md): `X-Forge-Event`, `X-Forge-Signature`). Нужен **точечный** consumer на стороне Host: «пришёл push → sync сейчас», без platform event bus.

## Decision

### 1. Принцип

| Режим | Роль |
|-------|------|
| **Push webhook (primary)** | `POST` на Host → тот же `GitCatalogSynchronizer` + reload UI |
| **Poll (safety net)** | редкий интервал (рекомендация **360–1440** мин), не 15 |

Poll **не удаляем**: webhook может не дойти (сеть, секрет, Host down).

### 2. Inbound endpoint (Host)

```
POST /v1/admin/catalog/sync
Content-Type: application/json
X-Forge-Event: push          # или catalog.sync (alias)
X-Forge-Signature: sha256=<hex>   # если секрет задан
```

Альтернатива auth (пилот): заголовок `X-Api-Key` = ingest/admin-подобный ключ Host (`[access]` / отдельный `[catalog_sync] api_key`), если Forge не шлёт HMAC.

**Поведение:**

1. Проверить подпись / API key.
2. Опционально отфильтровать: `repo` / `ref` / `branch` совпадают с `[catalog_git]` (иначе **204** без sync — чужой push).
3. Вызвать существующий sync (clone/fetch/reset) под lock (один sync за раз).
4. Если catalog bytes изменились → `CatalogSourceState.Replace` + `DevSpecReloadNotifier.Notify()` (как background service).
5. Ответ: **200** `{ "status": "ok", "changed": true|false, "commit": "…" }` или **202** если sync уже идёт.

Идемпотентность: повторный POST на тот же tip — `changed: false`, без лишнего reload.

### 3. Payload (совместим с Forge push)

Минимальный JSON (camelCase, как `CiWebhookService`):

```json
{
  "event": "push",
  "repo": "ursa-license-usage",
  "ref": "refs/heads/main",
  "branch": "main",
  "commit": "a1b2c3d4…",
  "pusher": "oar:Agent"
}
```

Поля `event` / `pusher` — optional. Host **не** парсит diff; всегда полный sync ветки из `[catalog_git]`.

Фильтр путей (`docs/dashspec/**`) — **non-goal v1** (усложняет forge); при шуме — отдельный outbound URL только для catalog-repo.

### 4. HMAC

Как у Forge CI:

- Секрет: `DASHSPEC_CATALOG_SYNC_SECRET` или `[catalog_git] sync_webhook_secret`.
- Подпись: `HMAC-SHA256(secret, rawBody)` → hex lower → заголовок `X-Forge-Signature: sha256=<hex>`.
- Сравнение — constant-time.
- Пустой секрет на Host в Production → **не** принимать неподписанные запросы (fail closed), кроме явного `[catalog_git] sync_allow_unsigned = true` для lab.

### 5. Конфиг Host (расширение ADR-0025)

```toml
[catalog_git]
enabled = true
url = "https://forge.example/<org>/<repo>.git"
branch = "main"
path = "docs/dashspec/catalogs/lus-prod.dashcatalog"
pull_interval_minutes = 720          # safety net; 15 — только lab
sync_webhook_secret = "<shared>"
# sync_repo_slug = "ursa-license-usage"   # optional filter vs payload.repo
```

Env: `DASHSPEC_CATALOG_GIT_PULL_MINUTES`, `DASHSPEC_CATALOG_SYNC_SECRET`, …

### 6. Non-goals

- Platform event bus / MassTransit / NATS / CloudEvents broker.
- Push body с file list / path filters (v1).
- Auto-discovery всех DashSpec Host в сети.
- Замена git remote на «Forge-only» API.

### 7. Security

- Endpoint **не** анонимный; не светить в `/health`.
- Только внутренняя сеть / FW на порт Host (как `:5295`).
- Sync выполняется LocalSystem/svc учётки Host — без произвольных URL из payload (URL только из TOML).

## Consequences

### Positive

- После push каталог обновляется за секунды.
- Poll редко — меньше нагрузка на git и диск.
- Переиспользуется код sync; контракт совпадает с Forge outbound headers.

### Negative / trade-offs

- Нужна настройка URL на стороне Forge (ADR-0067).
- Без webhook Host «тупой» до safety-net интервала — документировать.

## Alternatives considered

| Alternative | Why not (сейчас) |
|-------------|------------------|
| Оставить 15 мин poll | Шум и задержка UX. |
| Platform event lib | Один consumer — overkill. |
| CloudEvents + broker | Ops без выгоды при 1–2 listener’ах. |
| Только Forge plugin «знать» Host | URL уже есть в CI webhook pattern. |

## Implementation sketch (не в этом ADR)

1. Вынести sync+reload из background loop в `IGitCatalogSyncService`.
2. Background: delay → `SyncAsync`.
3. `MapPost("/v1/admin/catalog/sync", …)`.
4. Forge: fan-out `push` на `FORGE_DASHSPEC_WEBHOOK_URL` (или multi-URL list).

## See also

- Producer: [FORGE-ADR-0067](../../../../agent-forge/design/FORGE-ADR-0067-outbound-push-fanout.md) (путь относительно monorepo layout; иначе открыть sibling `agent-forge/design/`).
- Operator UI / WitDB settings: [ADR-0042](DASHSPEC-ADR-0042-host-control-center-witdb.md)
