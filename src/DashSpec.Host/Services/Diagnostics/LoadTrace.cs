using System.Diagnostics;

namespace DashSpec.Host.Services.Diagnostics;

public sealed class LoadTrace
{
    private readonly object _gate = new();
    private LoadTraceSnapshot? _last;
    private readonly List<LoadTraceSnapshot> _history = [];

    public LoadTraceSnapshot? Last
    {
        get
        {
            lock (_gate)
            {
                return _last;
            }
        }
    }

    public IReadOnlyList<LoadTraceSnapshot> History
    {
        get
        {
            lock (_gate)
            {
                return _history.ToList();
            }
        }
    }

    public LoadTraceSession Begin(string source) => new(this, source);

    internal void Complete(LoadTraceSnapshot snapshot)
    {
        lock (_gate)
        {
            _last = snapshot;
            _history.Add(snapshot);
            if (_history.Count > 20)
            {
                _history.RemoveAt(0);
            }
        }
    }
}

public sealed class LoadTraceSession(LoadTrace trace, string source) : IDisposable
{
    private readonly Stopwatch _total = Stopwatch.StartNew();
    private readonly List<LoadStepReport> _steps = [];
    private string? _error;
    private bool _completed;

    public void Step(string name, long elapsedMs, bool success, string? detail = null, string? error = null)
    {
        _steps.Add(new LoadStepReport(name, success, elapsedMs, detail, error));
    }

    public void Fail(string error)
    {
        _error = error;
        Complete(false);
    }

    public void Succeed() => Complete(true);

    public void Dispose()
    {
        if (!_completed)
        {
            Complete(string.IsNullOrWhiteSpace(_error));
        }
    }

    private void Complete(bool success)
    {
        if (_completed)
        {
            return;
        }

        _completed = true;
        _total.Stop();
        trace.Complete(new LoadTraceSnapshot(
            source,
            success && string.IsNullOrWhiteSpace(_error),
            _total.ElapsedMilliseconds,
            DateTimeOffset.UtcNow,
            _error,
            _steps.ToList()));
    }
}

public sealed record LoadStepReport(
    string Name,
    bool Success,
    long ElapsedMs,
    string? Detail = null,
    string? Error = null);

public sealed record LoadTraceSnapshot(
    string Source,
    bool Success,
    long TotalElapsedMs,
    DateTimeOffset FinishedAtUtc,
    string? Error,
    IReadOnlyList<LoadStepReport> Steps);
