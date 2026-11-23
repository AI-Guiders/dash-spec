using System.Diagnostics;
using System.Threading.Channels;
using DashSpec.Abstractions.Connectors;
using DashSpec.Abstractions.Data;
using DashSpec.Abstractions.Query;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OutWit.Database.AdoNet;

namespace DashSpec.Host.Services.Telemetry;

public sealed record QueryTelemetryEntry(
    DateTimeOffset TimestampUtc,
    string ConnectorId,
    string Kind,
    long DurationMs,
    int RowCount,
    bool Success,
    string SqlPreview,
    string? ErrorMessage);

public sealed record HostMetricSample(
    DateTimeOffset TimestampUtc,
    double WorkingSetMb,
    double ManagedHeapMb,
    int GcGen0,
    int GcGen1,
    int GcGen2,
    int ThreadPoolThreads);

public sealed record HostLogEntry(
    DateTimeOffset TimestampUtc,
    string Level,
    string Category,
    string Message,
    string? ExceptionText);

/// <summary>
/// ADR-0137: Non-blocking telemetry &amp; log store backed by <c>host-telemetry.witdb</c>.
/// Single background consumer writes batches sequentially so no write-write conflict ever occurs.
/// </summary>
public sealed class HostTelemetryStore : BackgroundService
{
    private readonly Channel<object> _channel = Channel.CreateBounded<object>(
        new BoundedChannelOptions(4096)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });

    private readonly Lock _memoryLock = new();
    private readonly LinkedList<QueryTelemetryEntry> _recentQueries = new();
    private readonly LinkedList<HostMetricSample> _recentMetrics = new();
    private readonly LinkedList<HostLogEntry> _recentLogs = new();
    private const int MaxInMemoryItems = 500;

    private string? _databasePath;
    private string? _connectionString;
    private long _totalQueries;
    private long _failedQueries;
    private long _totalQueryDurationMs;

    public string? DatabasePath => _databasePath;

    public string? ConnectionString => _connectionString;

    public string? RuntimeTomlPath { get; private set; }

    public string? SelfMonitoringSpecPath { get; private set; }

    public string? SelfMonitoringSpecText { get; private set; }

    public long TotalQueries => Interlocked.Read(ref _totalQueries);

    public long FailedQueries => Interlocked.Read(ref _failedQueries);

    public double AverageQueryMs =>
        TotalQueries is var total and > 0
            ? Math.Round((double)Interlocked.Read(ref _totalQueryDurationMs) / total, 1)
            : 0;

    public void InitializeDatabase(string hostSettingsDbPath)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(hostSettingsDbPath))
                  ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "DashSpec");
        Directory.CreateDirectory(dir);

        _databasePath = Path.Combine(dir, "host-telemetry.witdb");
        _connectionString = $"Data Source={_databasePath}";

        var runtimeTomlPath = Path.Combine(dir, "host-telemetry.runtime.toml");
        var escapedConn = _connectionString.Replace("\\", "\\\\");
        File.WriteAllText(
            runtimeTomlPath,
            $$"""
            [providers.witdb]
            connection_string = "{{escapedConn}}"
            command_timeout_seconds = 30
            max_rows = 50000
            """);
        RuntimeTomlPath = runtimeTomlPath;

        var selfMonitorDir = Path.Combine(dir, "self-monitoring");
        EnsureSelfMonitoringSpecCorpus(selfMonitorDir, runtimeTomlPath);

        using var conn = new WitDbConnection(_connectionString);
        conn.Open();

        ExecuteDdl(conn, """
            CREATE TABLE IF NOT EXISTS query_telemetry (
                TimestampUtc DATETIME NOT NULL,
                ConnectorId TEXT NOT NULL,
                Kind TEXT NOT NULL,
                DurationMs INTEGER NOT NULL,
                RowCount INTEGER NOT NULL,
                Status TEXT NOT NULL,
                SqlPreview TEXT NOT NULL,
                ErrorMessage TEXT NOT NULL
            );
            """);

        ExecuteDdl(conn, """
            CREATE TABLE IF NOT EXISTS host_metrics (
                TimestampUtc DATETIME NOT NULL,
                WorkingSetMb REAL NOT NULL,
                ManagedHeapMb REAL NOT NULL,
                GcGen0 INTEGER NOT NULL,
                GcGen1 INTEGER NOT NULL,
                GcGen2 INTEGER NOT NULL,
                ThreadPoolThreads INTEGER NOT NULL
            );
            """);

        ExecuteDdl(conn, """
            CREATE TABLE IF NOT EXISTS host_logs (
                TimestampUtc DATETIME NOT NULL,
                Level TEXT NOT NULL,
                Category TEXT NOT NULL,
                Message TEXT NOT NULL,
                ExceptionText TEXT NOT NULL
            );
            """);

        CaptureMetricSample();
    }

    public void RecordQuery(QueryTelemetryEntry entry)
    {
        if (string.Equals(entry.ConnectorId, "witdb", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Interlocked.Increment(ref _totalQueries);
        Interlocked.Add(ref _totalQueryDurationMs, entry.DurationMs);
        if (!entry.Success)
        {
            Interlocked.Increment(ref _failedQueries);
        }

        lock (_memoryLock)
        {
            _recentQueries.AddFirst(entry);
            while (_recentQueries.Count > MaxInMemoryItems)
            {
                _recentQueries.RemoveLast();
            }
        }

        _channel.Writer.TryWrite(entry);
    }

    public void RecordLog(HostLogEntry entry)
    {
        if (entry.Category.StartsWith("DashSpec.Host.Services.Telemetry", StringComparison.OrdinalIgnoreCase)
            || entry.Category.StartsWith("OutWit.Database", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        lock (_memoryLock)
        {
            _recentLogs.AddFirst(entry);
            while (_recentLogs.Count > MaxInMemoryItems)
            {
                _recentLogs.RemoveLast();
            }
        }

        _channel.Writer.TryWrite(entry);
    }

    public HostMetricSample CaptureMetricSample()
    {
        using var proc = Process.GetCurrentProcess();
        var workingSetMb = Math.Round(proc.WorkingSet64 / (1024.0 * 1024.0), 1);
        var managedHeapMb = Math.Round(GC.GetTotalMemory(forceFullCollection: false) / (1024.0 * 1024.0), 1);
        var sample = new HostMetricSample(
            DateTimeOffset.UtcNow,
            workingSetMb,
            managedHeapMb,
            GC.CollectionCount(0),
            GC.CollectionCount(1),
            GC.CollectionCount(2),
            ThreadPool.ThreadCount);

        lock (_memoryLock)
        {
            _recentMetrics.AddFirst(sample);
            while (_recentMetrics.Count > MaxInMemoryItems)
            {
                _recentMetrics.RemoveLast();
            }
        }

        _channel.Writer.TryWrite(sample);
        return sample;
    }

    public IReadOnlyList<QueryTelemetryEntry> GetRecentQueries(int max = 200)
    {
        lock (_memoryLock)
        {
            return _recentQueries.Take(max).ToList();
        }
    }

    public IReadOnlyList<HostMetricSample> GetRecentMetrics(int max = 120)
    {
        lock (_memoryLock)
        {
            return _recentMetrics.Take(max).ToList();
        }
    }

    public IReadOnlyList<HostLogEntry> GetRecentLogs(string? levelFilter = null, string? search = null, int max = 300)
    {
        lock (_memoryLock)
        {
            IEnumerable<HostLogEntry> query = _recentLogs;
            if (!string.IsNullOrWhiteSpace(levelFilter) && !string.Equals(levelFilter, "ALL", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x => string.Equals(x.Level, levelFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(x =>
                    x.Category.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || x.Message.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || (x.ExceptionText?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            return query.Take(max).ToList();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var metricsTimer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        var metricsTask = RunMetricsSamplerAsync(metricsTimer, stoppingToken);
        var writerTask = RunBatchWriterAsync(stoppingToken);
        await Task.WhenAll(metricsTask, writerTask).ConfigureAwait(false);
    }

    private async Task RunMetricsSamplerAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                CaptureMetricSample();
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task RunBatchWriterAsync(CancellationToken stoppingToken)
    {
        var batch = new List<object>(128);
        try
        {
            while (await _channel.Reader.WaitToReadAsync(stoppingToken).ConfigureAwait(false))
            {
                batch.Clear();
                while (batch.Count < 128 && _channel.Reader.TryRead(out var item))
                {
                    batch.Add(item);
                }

                if (batch.Count > 0 && !string.IsNullOrWhiteSpace(_connectionString))
                {
                    FlushBatchToWitDb(batch);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void FlushBatchToWitDb(List<object> batch)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return;
        }

        try
        {
            using var conn = new WitDbConnection(_connectionString);
            conn.Open();

            foreach (var item in batch)
            {
                switch (item)
                {
                    case QueryTelemetryEntry q:
                    {
                        using var cmd = new WitDbCommand("""
                            INSERT INTO query_telemetry (TimestampUtc, ConnectorId, Kind, DurationMs, RowCount, Status, SqlPreview, ErrorMessage)
                            VALUES (@ts, @conn, @kind, @dur, @rows, @status, @sql, @err);
                            """, conn);
                        cmd.Parameters.Add(new WitDbParameter("@ts", q.TimestampUtc));
                        cmd.Parameters.Add(new WitDbParameter("@conn", q.ConnectorId));
                        cmd.Parameters.Add(new WitDbParameter("@kind", q.Kind));
                        cmd.Parameters.Add(new WitDbParameter("@dur", q.DurationMs));
                        cmd.Parameters.Add(new WitDbParameter("@rows", q.RowCount));
                        cmd.Parameters.Add(new WitDbParameter("@status", q.Success ? "OK" : "ERROR"));
                        cmd.Parameters.Add(new WitDbParameter("@sql", q.SqlPreview));
                        cmd.Parameters.Add(new WitDbParameter("@err", q.ErrorMessage ?? string.Empty));
                        cmd.ExecuteNonQuery();
                        break;
                    }
                    case HostMetricSample m:
                    {
                        using var cmd = new WitDbCommand("""
                            INSERT INTO host_metrics (TimestampUtc, WorkingSetMb, ManagedHeapMb, GcGen0, GcGen1, GcGen2, ThreadPoolThreads)
                            VALUES (@ts, @ws, @heap, @g0, @g1, @g2, @tp);
                            """, conn);
                        cmd.Parameters.Add(new WitDbParameter("@ts", m.TimestampUtc));
                        cmd.Parameters.Add(new WitDbParameter("@ws", m.WorkingSetMb));
                        cmd.Parameters.Add(new WitDbParameter("@heap", m.ManagedHeapMb));
                        cmd.Parameters.Add(new WitDbParameter("@g0", m.GcGen0));
                        cmd.Parameters.Add(new WitDbParameter("@g1", m.GcGen1));
                        cmd.Parameters.Add(new WitDbParameter("@g2", m.GcGen2));
                        cmd.Parameters.Add(new WitDbParameter("@tp", m.ThreadPoolThreads));
                        cmd.ExecuteNonQuery();
                        break;
                    }
                    case HostLogEntry l:
                    {
                        using var cmd = new WitDbCommand("""
                            INSERT INTO host_logs (TimestampUtc, Level, Category, Message, ExceptionText)
                            VALUES (@ts, @lvl, @cat, @msg, @ex);
                            """, conn);
                        cmd.Parameters.Add(new WitDbParameter("@ts", l.TimestampUtc));
                        cmd.Parameters.Add(new WitDbParameter("@lvl", l.Level));
                        cmd.Parameters.Add(new WitDbParameter("@cat", l.Category));
                        cmd.Parameters.Add(new WitDbParameter("@msg", l.Message));
                        cmd.Parameters.Add(new WitDbParameter("@ex", l.ExceptionText ?? string.Empty));
                        cmd.ExecuteNonQuery();
                        break;
                    }
                }
            }
        }
        catch
        {
            // Non-fatal: in-memory ring buffer always retains the latest 500 entries.
        }
    }

    private static void ExecuteDdl(WitDbConnection conn, string sql)
    {
        using var cmd = new WitDbCommand(sql, conn);
        cmd.ExecuteNonQuery();
    }

    private void EnsureSelfMonitoringSpecCorpus(string selfMonitorDir, string runtimeTomlPath)
    {
        Directory.CreateDirectory(selfMonitorDir);
        var localRuntimeToml = Path.Combine(selfMonitorDir, "telemetry.runtime.toml");
        File.Copy(runtimeTomlPath, localRuntimeToml, overwrite: true);

        var typesAndFlowsDs = """
            module types host_telemetry_types
            namespace DashSpec.Std.Monitoring

            type TelemetryKpiRow
              val: int
            end type

            type TelemetryMemoryRow
              TimestampUtc: string
              WorkingSetMb: float
              ManagedHeapMb: float
              ThreadPoolThreads: int
            end type

            type TelemetryQueryRow
              TimestampUtc: string
              ConnectorId: string
              Kind: string
              DurationMs: int
              RowCount: int
              Status: string
              SqlPreview: string
            end type

            type TelemetryLogRow
              TimestampUtc: string
              Level: string
              Category: string
              Message: string
            end type

            end types host_telemetry_types

            module flow host_telemetry_flows
            namespace DashSpec.Std.Monitoring

            source src_metrics {
              use provider witdb
              from sql query "SELECT CAST(TimestampUtc AS TEXT) AS TimestampUtc, WorkingSetMb, ManagedHeapMb, ThreadPoolThreads FROM host_metrics ORDER BY TimestampUtc DESC"
              ports
                default output rows
                output stream rows: TelemetryMemoryRow
              end ports
            }

            source src_queries {
              use provider witdb
              from sql query "SELECT CAST(TimestampUtc AS TEXT) AS TimestampUtc, ConnectorId, Kind, DurationMs, RowCount, Status, SqlPreview FROM query_telemetry ORDER BY TimestampUtc DESC"
              ports
                default output rows
                output stream rows: TelemetryQueryRow
              end ports
            }

            source src_logs {
              use provider witdb
              from sql query "SELECT CAST(TimestampUtc AS TEXT) AS TimestampUtc, Level, Category, Message FROM host_logs ORDER BY TimestampUtc DESC"
              ports
                default output rows
                output stream rows: TelemetryLogRow
              end ports
            }

            end flow host_telemetry_flows
            """;

        File.WriteAllText(Path.Combine(selfMonitorDir, "telemetry-flows.ds"), typesAndFlowsDs);

        var diagramsDs = """
            module diagram diag_telemetry_queries
            namespace DashSpec.Std.Monitoring

            table
              columns
                column "TimestampUtc" "Time (UTC)" "text" size=content align=left
                column "ConnectorId" "Connector" "text" size=content align=center
                column "Kind" "Kind" "text" size=content align=center
                column "DurationMs" "Duration (ms)" "integer" size=content align=right
                column "RowCount" "Rows" "integer" size=content align=right
                column "Status" "Status" "text" size=content align=center
                column "SqlPreview" "SQL Preview" "text" size=fill align=left
              end columns
            end table

            end diagram diag_telemetry_queries

            module diagram diag_telemetry_logs
            namespace DashSpec.Std.Monitoring

            table
              columns
                column "TimestampUtc" "Time (UTC)" "text" size=content align=left
                column "Level" "Level" "text" size=content align=center
                column "Category" "Category" "text" size=content align=left
                column "Message" "Message" "text" size=fill align=left
              end columns
            end table

            end diagram diag_telemetry_logs
            """;

        File.WriteAllText(Path.Combine(selfMonitorDir, "telemetry-diagrams.ds"), diagramsDs);

        var reportDs = """
            module report self_monitoring
            namespace DashSpec.Std.Monitoring

            from DashSpec.Std import presentations = std_pres
            from DashSpec.Std.Monitoring import types = t_types, flows = t_flows, diagrams = t_diags

            runtime
              manifest = "telemetry.runtime.toml"
            end runtime

            connect
              connector = "witdb"
              dialect = "generic"
            end connect

            report "DashSpec Self-Monitoring (WitDB)"
              card recent_queries as "Executed Queries (WitDB: query_telemetry)"
                view
                  diagram ref diag_telemetry_queries table
                    use presentation table_compact
                    use flow src_queries
                  end table
                end view
              end card

              card recent_logs as "Host Event Log (WitDB: host_logs)"
                view
                  diagram ref diag_telemetry_logs table
                    use presentation table_detail
                    use flow src_logs
                  end table
                end view
              end card
            end report

            end report self_monitoring
            """;

        var reportPath = Path.Combine(selfMonitorDir, "self-monitoring.ds");
        File.WriteAllText(reportPath, reportDs);
        SelfMonitoringSpecPath = reportPath;
        SelfMonitoringSpecText = reportDs;
    }
}

/// <summary>
/// Wraps an <see cref="IDataSourceConnector"/> to record query latency, row count, and status into <see cref="HostTelemetryStore"/>.
/// </summary>
public sealed class TelemetryRecordingConnector(IDataSourceConnector inner, HostTelemetryStore store) : IDataSourceConnector
{
    public string Id => inner.Id;

    public async Task<TypedRowBatch> QueryAsync(CompiledQuery query, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var batch = await inner.QueryAsync(query, cancellationToken).ConfigureAwait(false);
            sw.Stop();
            store.RecordQuery(new QueryTelemetryEntry(
                DateTimeOffset.UtcNow,
                inner.Id,
                "card_query",
                sw.ElapsedMilliseconds,
                batch.Count,
                true,
                CompactSql(query.Sql),
                null));
            return batch;
        }
        catch (Exception ex)
        {
            sw.Stop();
            store.RecordQuery(new QueryTelemetryEntry(
                DateTimeOffset.UtcNow,
                inner.Id,
                "card_query",
                sw.ElapsedMilliseconds,
                0,
                false,
                CompactSql(query.Sql),
                ex.Message));
            throw;
        }
    }

    public async Task<IReadOnlyList<string>> QueryDistinctStringsAsync(string sql, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var values = await inner.QueryDistinctStringsAsync(sql, cancellationToken).ConfigureAwait(false);
            sw.Stop();
            store.RecordQuery(new QueryTelemetryEntry(
                DateTimeOffset.UtcNow,
                inner.Id,
                "field_options",
                sw.ElapsedMilliseconds,
                values.Count,
                true,
                CompactSql(sql),
                null));
            return values;
        }
        catch (Exception ex)
        {
            sw.Stop();
            store.RecordQuery(new QueryTelemetryEntry(
                DateTimeOffset.UtcNow,
                inner.Id,
                "field_options",
                sw.ElapsedMilliseconds,
                0,
                false,
                CompactSql(sql),
                ex.Message));
            throw;
        }
    }

    private static string CompactSql(string sql)
    {
        var compact = string.Join(' ', sql.Split(['\r', '\n', '\t', ' '], StringSplitOptions.RemoveEmptyEntries));
        return compact.Length <= 220 ? compact : compact[..220] + "…";
    }
}

public sealed class HostTelemetryLoggerProvider(HostTelemetryStore store) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new HostTelemetryLogger(categoryName, store);

    public void Dispose()
    {
    }

    private sealed class HostTelemetryLogger(string category, HostTelemetryStore store) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);
            if (string.IsNullOrWhiteSpace(message) && exception is null)
            {
                return;
            }

            store.RecordLog(new HostLogEntry(
                DateTimeOffset.UtcNow,
                logLevel.ToString(),
                category,
                message,
                exception?.ToString()));
        }
    }
}
