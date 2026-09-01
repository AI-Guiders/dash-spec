#nullable enable

using AIGuiders.Platform.Authoring.Command.Catalog;

namespace DashSpec.Host.Commands;

/// <summary>CCL invoker flavor from <c>defaults.command.flavor</c> + channel grammar (GUIDERS-ADR-0047).</summary>
internal static class DashboardCatalogFlavor
{
    public const string Console = "console";
    public const string Slash = "slash";
    public const string ConsoleGrammar = "command-console";
    public const string SlashGrammar = "command-slash";

    public static string InvokerFlavor =>
        ReadInvokerFlavor(DashboardCatalog.Current);

    public static bool IsConsole =>
        InvokerFlavor.Equals(Console, StringComparison.OrdinalIgnoreCase);

    public static CatalogChannel CclFilterChannel =>
        ResolveCclFilterChannel(DashboardCatalog.Current);

    public static string CclCommandGrammar =>
        CclFilterChannel.CommandGrammar ?? ConsoleGrammar;

    public static void ValidateAtLoad(CatalogDocument document)
    {
        if (!ReadInvokerFlavor(document).Equals(Console, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var cclGrammar = ResolveCclFilterChannel(document).CommandGrammar ?? ConsoleGrammar;
        if (!string.Equals(cclGrammar, ConsoleGrammar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"dash.catalog command.flavor is console but ccl.filter grammar is {cclGrammar}.");
        }
    }

    static string ReadInvokerFlavor(CatalogDocument document) =>
        string.IsNullOrWhiteSpace(document.Defaults.CommandFlavor)
            ? Console
            : document.Defaults.CommandFlavor.Trim();

    static CatalogChannel ResolveCclFilterChannel(CatalogDocument document) =>
        document.Channels.First(channel =>
            channel.Surface.Equals("ccl", StringComparison.OrdinalIgnoreCase)
            && channel.Sub?.Equals("filter", StringComparison.OrdinalIgnoreCase) == true);
}
