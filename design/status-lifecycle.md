# Статусы ADR: жизненный цикл (dash-spec)

Как в [Cascade IDE status-lifecycle](https://github.com/AI-Guiders/cascade-ide/blob/main/docs/adr/status-lifecycle.md): **первый тег** — решение принято или нет; **второй** (опционально) — внедрение в код.

## Первый тег (обязательный)

| Значение | Смысл |
|----------|--------|
| **Proposed** | Черновик на обсуждение. |
| **Accepted** | Норма для кода и ревью. |
| **Superseded** | Заменено другим ADR. |
| **Deprecated** | Не использовать для новых изменений. |

## Второй тег

Через **` · `** после Accepted:

- **`Accepted · Implemented`** — основная поставка в коде.
- **`Accepted (частично)`** или пояснение в шапке — strangler / не весь scope.

## Индекс [README.md](README.md)

В колонке **Status** — только lifecycle (**Accepted** / **Proposed** / **Superseded** / **Deprecated**). Детали реализации — в шапке файла ADR (`**Status:**`).

При смене статуса синхронизировать шапку ADR и строку индекса.
