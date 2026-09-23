using DashSpec.Abstractions.Plugins;
using DashSpec.Core.Data;

namespace DashSpec.Plugin.Export;

public sealed class XlsxExportActionHandler : IDashSpecActionHandler
{
    public string ActionId => "xlsx_export";

    public ValueTask<DashSpecActionOutcome> ExecuteAsync(
        DashSpecActionContext context,
        IReadOnlyDictionary<string, string> args,
        CancellationToken cancellationToken = default)
    {
        if (context.Table is null || context.Table.Columns.Count == 0)
        {
            return ValueTask.FromResult(new DashSpecActionOutcome());
        }

        var bytes = XlsxWorkbook.Write(context.Table.Columns, context.Table.Rows);
        var fileName = args.GetValueOrDefault("filename") ?? $"{SanitizeFileName(context.CardId)}.xlsx";
        if (!fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            fileName += ".xlsx";
        }

        return ValueTask.FromResult(new DashSpecActionOutcome(
            DashSpecActionOutcomeKind.DownloadBase64,
            fileName,
            Convert.ToBase64String(bytes),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"));
    }

    private static string SanitizeFileName(string cardId)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new System.Text.StringBuilder(cardId.Length);
        foreach (var ch in cardId)
        {
            builder.Append(invalid.Contains(ch) ? '_' : ch);
        }

        return builder.Length == 0 ? "export" : builder.ToString();
    }
}
