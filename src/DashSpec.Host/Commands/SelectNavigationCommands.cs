#nullable enable
using AIGuiders.Platform.CommandPlane.Commands;

namespace DashSpec.Host.Commands;

internal sealed class SelectReportCommand : PlatformCommand<DashboardFilterContext>
{
    public const string Id = "dash.select.report";

    public override string CommandId => Id;

    protected override CommandOutcome Execute(DashboardFilterContext context)
    {
        var entryId = context.ArgTail.Trim();
        if (entryId.Length == 0)
        {
            entryId = FilterCommandPaths.ReadBranchArg(context.CanonicalPath, FilterCommandPaths.ReportBranch) ?? "";
        }

        if (entryId.Length == 0)
        {
            return CommandOutcome.Fail("Укажите отчёт: select report <id>.");
        }

        if (context.CatalogEntries.All(entry =>
                !string.Equals(entry.Id, entryId, StringComparison.OrdinalIgnoreCase)))
        {
            return CommandOutcome.Fail($"Неизвестный отчёт '{entryId}'.");
        }

        context.PendingCatalogEntryId = entryId;
        return CommandOutcome.Ok();
    }
}

internal sealed class SelectPageCommand : PlatformCommand<DashboardFilterContext>
{
    public const string Id = "dash.select.page";

    public override string CommandId => Id;

    protected override CommandOutcome Execute(DashboardFilterContext context)
    {
        var pageId = context.ArgTail.Trim();
        if (pageId.Length == 0)
        {
            pageId = FilterCommandPaths.ReadBranchArg(context.CanonicalPath, FilterCommandPaths.PageBranch) ?? "";
        }

        if (pageId.Length == 0)
        {
            return CommandOutcome.Fail("Укажите страницу: select page <id>.");
        }

        if (context.ReportPages.All(page =>
                !string.Equals(page.Id, pageId, StringComparison.OrdinalIgnoreCase)))
        {
            return CommandOutcome.Fail($"Неизвестная страница '{pageId}'.");
        }

        context.PendingPageId = pageId;
        return CommandOutcome.Ok();
    }
}
