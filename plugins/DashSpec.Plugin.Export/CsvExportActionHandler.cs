using System.Text;
using DashSpec.Abstractions.Plugins;

namespace DashSpec.Plugin.Export;

public sealed class CsvExportActionHandler : IDashSpecActionHandler
{
    public string ActionId => "csv_export";

    public ValueTask<DashSpecActionOutcome> ExecuteAsync(
        DashSpecActionContext context,
        IReadOnlyDictionary<string, string> args,
        CancellationToken cancellationToken = default)
    {
        if (context.Table is null || context.Table.Columns.Count == 0)
        {
            return ValueTask.FromResult(new DashSpecActionOutcome());
        }

        var delimiter = args.GetValueOrDefault("delimiter") ?? ";";
        if (delimiter.Length != 1)
        {
            delimiter = ";";
        }

        var separator = delimiter[0];
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(separator, context.Table.Columns.Select(EscapeCsvField)));

        foreach (var row in context.Table.Rows)
        {
            builder.AppendLine(string.Join(separator, row.Select(EscapeCsvField)));
        }

        var fileName = args.GetValueOrDefault("filename")
            ?? $"{SanitizeFileName(context.CardId)}.csv";

        return ValueTask.FromResult(new DashSpecActionOutcome(
            DashSpecActionOutcomeKind.DownloadText,
            fileName,
            builder.ToString(),
            "text/csv;charset=utf-8"));
    }

    private static string EscapeCsvField(string? value)
    {
        var text = value ?? string.Empty;
        if (text.Contains('"') || text.Contains('\n') || text.Contains('\r') || text.Contains(';') || text.Contains(','))
        {
            return $"\"{text.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }

        return text;
    }

    private static string SanitizeFileName(string cardId)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(cardId.Length);
        foreach (var ch in cardId)
        {
            builder.Append(invalid.Contains(ch) ? '_' : ch);
        }

        return builder.Length == 0 ? "export" : builder.ToString();
    }
}
