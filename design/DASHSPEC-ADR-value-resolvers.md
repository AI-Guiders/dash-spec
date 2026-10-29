---
type: ADR
id: DASHSPEC-ADR-value-resolvers
title: Value Resolvers — from_* expressions for external data sources
status: Proposed
date: 2026-10-30
tags: [dashspec, adr, runtime, plugins, configuration, value-resolvers]
language: en
relates:
  - DASHSPEC-ADR-0032-extension-blocks-and-plugins
  - DASHSPEC-ADR-0059-vertical-viz-plugins
  - DASHSPEC-ADR-0060-vertical-filter-plugins
  - DASHSPEC-ADR-0073-author-navigation-surface
federation:
  - GUIDERS-ADR-0064 (*.config.gdl: ConfigSourceRow { Id, Kind, Path, Slice })
---

# Value Resolvers — `from_*` expressions for external data sources

## Context

Спека — SSOT для **структуры** отчёта, но не для **секретов и среды**. Сегодня:

- `runtime { manifest = "lus-runtime.toml" }` — ссылка на TOML с провайдерами, плагинами, connection strings
- Connection strings с паролями — в git (`.toml` рядом со спекой), что неправильно
- Разные среды (dev/prod) — разные TOML файлы, спека не переносима между ними
- Язык не умеет выражать «возьми значение из внешнего источника»

Плагины уже умеют расширять язык (phrase templates, extension blocks), но нет механизма для
**value-level интеграции** — «значение этого свойства придёт из вне».

## Decision

### Value Resolver — плагин-функция в property values

Плагин регистрирует resolver — функцию, которая читает внешний источник и возвращает значение.
Парсер встречает не-literал в значении свойства — вызывает resolver, подставляет результат.

### Синтаксис

```ds
runtime
  provider sql
    connection_string = from_toml("rt.toml", "providers.sql.connection_string")
    timeout = from_toml("rt.toml", "providers.sql.timeout")
  end provider
end runtime
```

Или через именованный source (читает файл один раз):

```ds
runtime
  source config = from_toml("rt.toml")
  source secrets = from_env()

  provider sql
    connection_string = config["providers.sql.connection_string"]
    timeout = config["providers.sql.timeout"]
    password = secrets["LUS_DB_PASSWORD"]
  end provider
end runtime
```

### Контракт

```csharp
/// <summary>Plugin-registered value resolver (ADR-XXXX).</summary>
public interface IValueResolver
{
    /// <summary>Resolver id: "toml", "json", "env", "yaml", "vault", …</summary>
    string Id { get; }

    /// <summary>Open a source; returns a lookup function.</summary>
    /// <param name="path">File path, env prefix, or plugin-specific locator.</param>
    /// <returns>Key lookup — dot-notation path → string value.</returns>
    Func<string, string?> Open(string? path);
}
```

Плагин реализует интерфейс, регистрирует в `IDashSpecContributorRegistry`:

```csharp
registry.RegisterValueResolver(new TomlValueResolver());
registry.RegisterValueResolver(new EnvValueResolver());
```

### Парсер

`PropertyBlockParser` расширяется: если значение свойства не literaл (не string/int/bool),
а начинается с `from_<id>(` или `<source_var>[` — это resolver-вызов:

1. **Inline**: `from_toml("rt.toml", "key.path")` → открыть, прочитать, подставить
2. **Named source**: `source config = from_toml("rt.toml")` → открыть, кешировать; дальше `config["key.path"]`
3. **Env direct**: `from_env("VAR_NAME")` → `Environment.GetEnvironmentVariable("VAR_NAME")`

### Встроенные resolver'ы (stdlib)

| ID | Источник | Пример |
|----|----------|--------|
| `toml` | TOML файл (Tomlyn) | `from_toml("rt.toml", "providers.sql.timeout")` |
| `json` | JSON файл (System.Text.Json) | `from_json("cfg.json", "limits.max_rows")` |
| `env` | Environment variables | `from_env("LUS_DB_PASSWORD")` |
| `yaml` | YAML файл (YamlDotnet) | `from_yaml("cfg.yaml", "db.host")` |

Дополнительные (`vault`, `consul`, `aws_secrets`) — сторонние плагины, тот же интерфейс.

### Где допустимы

| Блок | Допустимы? | Причина |
|------|-----------|---------|
| `runtime { }` | **да** | Провайдеры, connection strings — основной use case |
| `configuration { }` | **да** | Настройки, dialect, timezone |
| `card { }` (свойства) | **нет** v1 | Отчёт должен быть детерминированным; данные приходят через flow |
| `filters { }` | **нет** v1 | Значения фильтров — от пользователя, не от среды |

### Разделение: спека vs секреты

```ds
// Спека (git): структура, ID провайдеров, что включить
runtime
  provider sql
    connection_string = from_env("LUS_DB")
    timeout = from_toml("rt.toml", "sql.timeout")
  end provider
end runtime
```

```
// Среда (не в git): значения
LUS_DB=Server=prod;Database=...;Password=...
rt.toml: { sql: { timeout: 120 } }
```

Одна спека — разные среды через разные env vars и config files.

## Implementation phases

| Phase | Deliverable |
|-------|-------------|
| **P0** | ADR + interface `IValueResolver` |
| **P1** | Парсер: `from_*(path, key)` inline в `runtime` и `configuration`; stdlib resolvers (toml, json, env) |
| **P2** | Named sources (`source config = from_toml(...)`); resolver registry в plugin pipeline |
| **P3** | YAML, дополнительные источники; рантайм-провайдеры полностью в DSL (TOML-манифест умирает) |

## Consequences

- Язык выражает **откуда** значение, не **что** в нём — секреты не в git
- Новые источники — плагины, ноль изменений в языке для каждого
- `IConfiguration`-паттерн: dot-notation путь, единый lookup
- TOML-манифест эволюционирует в DSL `runtime`-блок с resolver'ами
- Спека становится переносимой между средами (одна спека, разные env/config)
