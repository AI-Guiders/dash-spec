namespace DashSpec.Abstractions.Plugins;

public sealed record DashSpecTableData(
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<string>> Rows);

public sealed record DashSpecActionContext(
    string CardId,
    string CardTitle,
    DashSpecTableData? Table);

public enum DashSpecActionOutcomeKind
{
    None,
    DownloadText,
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
