using DashSpec.Core.Analysis;
using DashSpec.Core.Catalog;
using DashSpec.Host.Plugins;
using DashSpecParser = DashSpec.Execution.Parsing.DashSpecParser;

namespace DashSpec.Host.Services.Health;

public sealed class DashSpecCatalogHealthService(
    CatalogSourceState catalogState,
    DashSpecParseOptionsProvider parseOptionsProvider)
{
    public DashSpecHealthReport Evaluate()
    {
        var errors = new List<string>();
        var catalog = catalogState.Current;

        foreach (var entry in catalog.Document.Entries)
        {
            try
            {
                var specPath = catalog.ResolveEntrySpecFullPath(entry.Id);
                if (!File.Exists(specPath))
                {
                    errors.Add($"[{entry.Id}] DashSpec file not found: {specPath}");
                    continue;
                }

                var text = File.ReadAllText(specPath);
                var document = DashSpecParser.Parse(
                    text,
                    Path.GetDirectoryName(specPath),
                    parseOptionsProvider.CreateOptions());
                foreach (var message in DashboardValidationCollector.Collect(document))
                {
                    errors.Add($"[{entry.Id}] {message}");
                }
            }
            catch (Exception ex)
            {
                AppendEntryErrors(errors, entry.Id, ex.Message);
            }
        }

        return new DashSpecHealthReport(errors.Count == 0, errors);
    }

    private static void AppendEntryErrors(List<string> errors, string entryId, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            errors.Add($"[{entryId}] Unknown error.");
            return;
        }

        foreach (var line in message.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            errors.Add($"[{entryId}] {line}");
        }
    }
}

public sealed record DashSpecHealthReport(bool DashboardLoadable, IReadOnlyList<string> DashboardErrors);
