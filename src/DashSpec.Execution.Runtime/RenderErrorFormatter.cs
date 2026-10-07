using DashSpec.Core.Model;

namespace DashSpec.Execution.Runtime;

/// <summary>Human-readable card/slot render failures for Host UI (not bare ADO column tokens).</summary>
public static class RenderErrorFormatter
{
    public static string ForCard(Exception exception, CardDefinition card, string? slotRef = null)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(card);

        var message = ResolveMessage(exception);
        message = EnrichBareFieldToken(message, card);

        var prefix = string.IsNullOrWhiteSpace(slotRef)
            ? $"Card '{card.Id}'"
            : $"Card '{card.Id}', diagram slot '{slotRef}'";

        if (!string.IsNullOrWhiteSpace(card.DataSource.RowsType))
        {
            prefix += $", row type '{card.DataSource.RowsType}'";
        }

        if (!string.IsNullOrWhiteSpace(card.DataSource.Value))
        {
            prefix += $", source '{DescribeDataSource(card.DataSource)}'";
        }

        return $"{prefix}: {message}";
    }

    private static string DescribeDataSource(DataSourceDefinition source) =>
        source.Kind switch
        {
            DataSourceKind.View => source.Value,
            DataSourceKind.Sql => "SQL",
            DataSourceKind.Xlsx => source.Value,
            _ => source.Value,
        };

    private static Exception Unwrap(Exception exception)
    {
        var current = exception;
        while (current is System.Reflection.TargetInvocationException { InnerException: not null } tie)
        {
            current = tie.InnerException!;
        }

        return current;
    }

    private static string ResolveMessage(Exception exception)
    {
        var current = Unwrap(exception);
        var message = string.IsNullOrWhiteSpace(current.Message) ? string.Empty : current.Message.Trim();
        if (LooksLikeBareIdentifier(message) && current.InnerException is not null)
        {
            var inner = current.InnerException.Message.Trim();
            if (!string.IsNullOrWhiteSpace(inner) && !LooksLikeBareIdentifier(inner))
            {
                message = inner;
            }
        }

        return string.IsNullOrWhiteSpace(message) ? "Unknown render error." : message;
    }

    private static string EnrichBareFieldToken(string message, CardDefinition card)
    {
        if (!LooksLikeBareIdentifier(message))
        {
            return message;
        }

        var rowType = string.IsNullOrWhiteSpace(card.DataSource.RowsType)
            ? "row type"
            : $"row type '{card.DataSource.RowsType}'";
        var source = string.IsNullOrWhiteSpace(card.DataSource.Value)
            ? "SQL query"
            : $"source '{DescribeDataSource(card.DataSource)}'";

        return
            $"SQL result has no column '{message}' required by {rowType} ({source}). " +
            "Align dashflow/dashtype fields with view columns, or extend the view.";
    }

    private static bool LooksLikeBareIdentifier(string message) =>
        message.Length > 0 &&
        message.Length <= 128 &&
        !message.Contains(' ', StringComparison.Ordinal) &&
        !message.Contains('\n', StringComparison.Ordinal) &&
        message.All(static c => char.IsLetterOrDigit(c) || c is '_' or '.');
}
