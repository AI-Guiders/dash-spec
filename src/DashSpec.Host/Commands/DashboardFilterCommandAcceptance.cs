#nullable enable

using AIGuiders.Platform.CommandPlane;
using DashSpec.Host.Commands.Constructors;

namespace DashSpec.Host.Commands;

internal static class DashboardFilterCommandAcceptance
{
    public static bool TryAcceptItem(
        SlashCompletionItem item,
        DashboardFilterCommandService commandService,
        DashboardFilterContext context,
        DashboardSlashConstructorHost constructorHost,
        ref string line)
    {
        var catalog = commandService.BuildCatalog(context);

        if (item.Kind == SlashCompletionItemKind.ConstructorEntry)
        {
            if (!DashboardFilterSlashCompletion.TryResolveCommandPath(catalog, line, out var path))
            {
                return false;
            }

            constructorHost.Session.Start(item.PickValue!, path);
            return true;
        }

        if (item.Kind == SlashCompletionItemKind.ConstructorStep)
        {
            constructorHost.Session.TryAdvance(item.PickValue!);
            if (constructorHost.Session.TryComplete(out var wire)
                && DashboardFilterSlashCompletion.TryResolveCommandPath(catalog, line, out var path))
            {
                line = $"{path} {wire}";
            }

            return true;
        }

        line = DashboardFilterSlashCompletion.LineFromInsert(item.InsertText);
        if (item.InsertText.EndsWith(' '))
        {
            line += " ";
        }

        return true;
    }

    public static void CancelConstructorIfActive(DashboardSlashConstructorHost constructorHost)
    {
        if (constructorHost.Session.IsActive)
        {
            constructorHost.Session.Cancel();
        }
    }
}
