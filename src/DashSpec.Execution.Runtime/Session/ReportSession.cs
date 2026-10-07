using DashSpec.Core.Model;
using DashSpec.Core.Platform;
using DashSpec.Execution.Runtime.Platform;
using DashSpec.Core.Runtime;

namespace DashSpec.Execution.Runtime.Session;

/// <summary>Headless session over <see cref="IReportSpecBootstrap"/> (ADR-0099 B2).</summary>
public sealed class ReportSession(IReportSpecBootstrap bootstrap)
{
    private DashboardDocument? _document;
    private FilterState? _filters;
    private IReadOnlyDictionary<string, FilterDefinition>? _filterIndex;
    private IReadOnlyDictionary<string, IReadOnlyList<string>> _fieldOptions =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

    public DashboardDocument Document => _document ?? throw new InvalidOperationException("Report not loaded.");
    public FilterState Filters => _filters ?? throw new InvalidOperationException("Report not loaded.");
    public IReadOnlyDictionary<string, FilterDefinition> FilterIndex =>
        _filterIndex ?? throw new InvalidOperationException("Report not loaded.");

    public async Task LoadFromTextAsync(
        string text,
        string specFullPath,
        CancellationToken cancellationToken = default,
        ReportLoadOptions? options = null)
    {
        var result = await bootstrap
            .LoadFromTextAsync(text, specFullPath, Path.GetFileName(specFullPath), cancellationToken, options)
            .ConfigureAwait(false);
        _document = result.Document;
        _filters = result.Filters;
        _filterIndex = result.FilterIndex;
        _fieldOptions = result.FieldOptions;
    }

    public void ApplyDateFilter(string name, DateOnly from, DateOnly to) => Filters.SetDate(name, from, to);

    public IReadOnlyList<string> GetFieldOptions(string filterName) =>
        _fieldOptions.TryGetValue(filterName, out var values) ? values : [];
}
