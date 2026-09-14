using DashSpec.Core.Model;

namespace DashSpec.Core.Parsing;

/// <summary>Runtime hook for F# document SSOT without Core→Modeling.Parse cycle.</summary>
internal static class DocumentParseBridge
{
    internal static Func<string, string?, DashSpecParseOptions, DashboardDocument>? Parse { get; set; }

    internal static Func<string, string?>? ReadRuntimePath { get; set; }

    internal static Func<string, string?>? ReadConfigPath { get; set; }

    internal static Func<string, string?>? ReadDiagramLibraryPath { get; set; }

    internal static Func<string, string?>? ReadPalettePath { get; set; }

    internal static Func<string, SqlDialect>? ReadSqlDialect { get; set; }

    internal static Func<string, (string Id, string Title)>? ReadDashboardHeader { get; set; }

    internal static Func<string, bool>? IsBlockModuleFormat { get; set; }

    internal static Func<string, bool>? IsTabRootDocument { get; set; }
}
