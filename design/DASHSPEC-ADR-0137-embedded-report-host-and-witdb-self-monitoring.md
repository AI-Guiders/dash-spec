---
type: ADR
id: DASHSPEC-ADR-0137
title: "Встраиваемая поверхность отчёта (Embedded Report Surface), коннектор WitDB и самодиагностика хоста (Self-Monitoring & Logs)"
status: "Proposed"
implementation: "Целевая архитектура: компонент <DashSpecEmbeddedReport /> с изолированным DashboardSession/DashboardPageController для встраивания любого .ds-отчёта в любую страницу хоста (/admin/monitoring, /studio, модальные панели); коннектор DashSpec.Connector.WitDb (id = witdb); асинхронный неблокирующий сборщик телеметрии и логов через Channel<T> в host-telemetry.witdb (query_telemetry, host_metrics, host_logs) и встроенный отчёт stdlib/reports/self_monitoring.ds."
date: 2026-11-23
tags: [dashspec, adr, witdb, telemetry, logging, self-monitoring, connector, embedded-report]
language: ru-RU
relates:
  - DASHSPEC-ADR-0042
  - DASHSPEC-ADR-0085
  - DASHSPEC-ADR-0111
  - DASHSPEC-ADR-0133
  - DASHSPEC-ADR-0135
---

# DASHSPEC-ADR-0137: Встраиваемая поверхность отчёта (`Embedded Report Surface`), коннектор `WitDB` и самодиагностика хоста (`Self-Monitoring & Logs`)

## Context

1. **Потребность в наблюдении за работой самого `DashSpec.Host` без чтения консоли и Event Viewer.**
   Сейчас время выполнения SQL-запросов, попадания в кеш (`FieldOptionsCache`), потребление памяти (`Working Set` / `Managed Heap`), ошибки подключения к источникам данных и системные предупреждения пишутся только в текстовый `ILogger` (консоль / Windows Event Log). Администратору и автору отчётов на сервере неудобно диагностировать медленные карточки, ошибки коннекторов или всплески памяти.

2. **Собственная реляционная СУБД `WitDatabase` уже встроена в хост.**
   По `DASHSPEC-ADR-0042` в `DashSpec.Host` уже работает встроенный движок `OutWit.Database` (`WitDB`) с полноценным ADO.NET-провайдером (`OutWit.Database.AdoNet`: `WitDbConnection`, `WitDbCommand`, `DbDataReader`). При этом материализатор `SqlRowMaterializer` в `DashSpec.Abstractions` работает поверх стандартного `DbDataReader`.

3. **Рендеринг `.ds`-отчётов не должен быть привязан только к корневому маршруту `/`.**
   Ядро отображения (`DashboardTabBoard`, `HostTabBoardSlot`, `DashboardCardSlot`, `DashboardCardView`) зависит только от экземпляра `DashboardPageController` и `DashboardSession`, а не от URL страницы. Это позволяет рендерить полноценный `.ds`-дашборд (с фильтрами, графиками, матрицами, дриллом и таблицами с копированием столбцов) внутри любой страницы — в частности, прямо в разделе `/admin/monitoring` (Control Center) или в системной вкладке.

## Decision

### 1. Встраиваемый компонент `<DashSpecEmbeddedReport />`

В `DashSpec.Surface.Blazor` выделяется самодостаточный компонент **`<DashSpecEmbeddedReport />`**, который создаёт изолированный экземпляр `DashboardSession` и `DashboardPageController` (не перезаписывая глобальный `CatalogUsage` зрителя на `/`) и рендерит переданный `.ds`-модуль (из файла или встроенного ресурса `stdlib/reports/*.ds`):

```razor
<DashSpecEmbeddedReport SpecSource="@EmbeddedSpecSource.FromStdlib("reports/self_monitoring.ds")"
                        ShowCatalogSelector="false" />
```

Это позволяет размещать полноценные интерактивные `.ds`-отчёты:
- внутри Control Center (`/admin/monitoring` и `/admin/logs`);
- внутри `/studio` (предпросмотр и профилировщик запросов);
- на любых пользовательских или плагинных маршрутах.

### 2. Коннектор `DashSpec.Connector.WitDb` (`provider = "witdb"`)

Создаётся встроенный коннектор **`DashSpec.Connector.WitDb`** (`IDataSourceConnector`, `Id = "witdb"`):
- использует `WitDbConnection` и `WitDbCommand` из `OutWit.Database.AdoNet`;
- материализует выборки через общий `SqlRowMaterializer.ReadRow(reader, schema, columnOrdinals, query.DateTimeFrame)` в `TypedRowBatch`;
- поддерживает как внешние `.witdb` файлы каталога, так и системный источник `host-telemetry.witdb`.

### 3. Архитектура сбора телеметрии и логов в `host-telemetry.witdb`

Чтобы запись логов и метрик никогда не блокировала UI-потоки Blazor, не конкурировала с `host-settings.witdb` и не вызывала `write-write conflict` при параллельных запросах:

1. **Отдельный файл БД `host-telemetry.witdb`** рядом с `host-settings.witdb`.
2. **Единый асинхронный писатель (`HostTelemetryWriterService : BackgroundService`) поверх `Channel<TelemetryEvent>` (`BoundedChannelFullMode.DropOldest`)**:
   - Все продюсеры (обёртка коннектора, `ISpecLoadTraceSink`, `ILoggerProvider`, сэмплер памяти/CPU) не ходят в БД напрямую — они за наносекунды кладут запись в неблокирующий `Channel.Writer.TryWrite(...)`.
   - Единственный фоновый consumer-поток вычитывает пачки (batch до 100 записей или раз в 500 мс) и записывает их в `host-telemetry.witdb` в одной транзакции. Поскольку писатель строго один, конфликты транзакций (`write-write conflict`) исключены по построению.
3. **Защита от рекурсии (`SkipTelemetry`):**
   - Запросы самого коннектора `witdb` к `host-telemetry.witdb` и логи категории `DashSpec.Telemetry.*` помечаются флагом `SkipTelemetry`, чтобы просмотр дашборда мониторинга не порождал новые записи о самом себе.
4. **Автоматическая ротация (Retention Policy):**
   - Фоновый писатель раз в час удаляет записи старше настраиваемого окна хранения (по умолчанию 7 дней или `max_rows = 100_000` на таблицу).

### 4. Схема таблиц `host-telemetry.witdb`

1. **`query_telemetry`** — статистика каждого запроса карточки / фильтра / загрузки спецификации:
   - `Id` (`INTEGER PRIMARY KEY`), `TimestampUtc` (`TEXT`), `CatalogEntryId` (`TEXT`), `ReportId` (`TEXT`), `CardId` (`TEXT`), `FlowId` (`TEXT`), `ConnectorId` (`TEXT`), `Kind` (`TEXT`: `card_query | field_options | spec_compile`), `DurationMs` (`INTEGER`), `RowCount` (`INTEGER`), `CacheHit` (`INTEGER`), `Status` (`TEXT`: `ok | error | timeout`), `ErrorMessage` (`TEXT`).
2. **`host_metrics`** — периодический срез ресурсов процесса `DashSpec.Host` (каждые 15 секунд):
   - `TimestampUtc` (`TEXT`), `WorkingSetMb` (`REAL`), `ManagedHeapMb` (`REAL`), `GcGen0` (`INTEGER`), `GcGen1` (`INTEGER`), `GcGen2` (`INTEGER`), `CpuPercent` (`REAL`), `ActiveCircuits` (`INTEGER`), `ThreadPoolThreads` (`INTEGER`).
3. **`host_logs`** — структурированный журнал событий хоста (`Information`, `Warning`, `Error`, `Critical`):
   - `Id` (`INTEGER PRIMARY KEY`), `TimestampUtc` (`TEXT`), `Level` (`TEXT`), `Category` (`TEXT`), `Message` (`TEXT`), `ExceptionText` (`TEXT`).

### 5. Встроенный `.ds`-отчёт `stdlib/reports/self_monitoring.ds`

Отчёт самодиагностики пишется на стандартном языке DashSpec (`namespace DashSpec.Std.Monitoring`) и включает три вкладки (`tab`):
1. **Обзор и ресурсы (`overview`):**
   - KPI-карточки: `Working Set (MB)`, `Managed Heap (MB)`, `Active Sessions`, `Avg Query (ms)`, `Cache Hit (%)`, `Errors (24h)`.
   - Линейные графики (`line`, `grid_lines = both`): динамика памяти/GC и времени ответа запросов по времени.
2. **Статистика запросов (`queries`):**
   - Топ самых долгих карточек и потоков (`bar`) с кликом в компактную дрилл-таблицу (`use table_compact`) конкретных запусков.
   - Статистика популярности отчётов каталога (из `catalog_usage`).
3. **Журнал событий (`logs`):**
   - Детальная таблица (`use table_detail`) по `host_logs` с фильтрами по `Level` (`Error`, `Warning`, `Information`), `Category`, дате/времени и кнопкой быстрого копирования столбца/ошибки.

## Consequences

- **Self-Hosting (Dogfooding):** DashSpec использует собственные `.ds`-диаграммы, фильтры, таблицы и `WitDB` для мониторинга своей же производительности и просмотра логов.
- **Нулевой оверхед на горячем пути и отсутствие блокировок БД:** неблокирующий `Channel<T>` и единственный фоновый batch-writer гарантируют отсутствие конкуренции за `WitDB`.
- **Повторное использование `<DashSpecEmbeddedReport />`:** любой отчёт теперь можно встроить в админку, Studio или стороннюю страницу хоста.
