using DashSpec.Abstractions.Data;

namespace DashSpec.Abstractions.Plugins;

/// <summary>Display table for action handlers (copy/export). Rows carry the formatted display
/// strings; TypedRows/ColumnKinds ride parallel for typed exports (xlsx numbers) — ADR-0079 §3.</summary>
public sealed record DashSpecTableData(
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<string>> Rows,
    IReadOnlyList<IReadOnlyList<DashValue>>? TypedRows = null,
    IReadOnlyList<DashPrimitiveKind>? ColumnKinds = null);

public sealed record DashSpecActionContext(
    string CardId,
    string CardTitle,
    DashSpecTableData? Table);

public enum DashSpecActionOutcomeKind
{
    None,
    DownloadText,
    DownloadBase64,
    RefreshCard,
}

public sealed record DashSpecActionOutcome(
    DashSpecActionOutcomeKind Kind = DashSpecActionOutcomeKind.None,
    string? FileName = null,
    string? TextContent = null,
    string? MimeType = null,
    string? RefreshCardId = null);

public interface IDashSpecActionHandler
{
    string ActionId { get; }

    ValueTask<DashSpecActionOutcome> ExecuteAsync(
        DashSpecActionContext context,
        IReadOnlyDictionary<string, string> args,
        CancellationToken cancellationToken = default);
}
