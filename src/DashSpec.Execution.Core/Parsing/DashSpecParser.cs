using DashSpec.Core.Model;
using DashSpec.Core.Parsing;

namespace DashSpec.Execution.Parsing;

/// <summary>
/// Stable parse facade for Host, Studio, and tests (ADR-0048 §6).
/// Routes document parsing through F# Modeling.Parse via <see cref="DocumentParseBridge"/>.
/// </summary>
public static class DashSpecParser
{
    static DashSpecParser() => ModuleParseRegistration.EnsureRegistered();

    /// <summary>Register F# parse bridges (tests and hosts without full parse).</summary>
    public static void EnsureModuleParsersRegistered() => ModuleParseRegistration.EnsureRegistered();

    public static string? ReadRuntimePath(string text) =>
        RequireBridge(DocumentParseBridge.ReadRuntimePath, "ReadRuntimePath")(text);

    [Obsolete("Use ReadRuntimePath. @config is a deprecated alias for @runtime.")]
    public static string? ReadConfigPath(string text) =>
        RequireBridge(DocumentParseBridge.ReadConfigPath, "ReadConfigPath")(text);

    public static string? ReadDiagramLibraryPath(string text) =>
        RequireBridge(DocumentParseBridge.ReadDiagramLibraryPath, "ReadDiagramLibraryPath")(text);

    public static string? ReadPalettePath(string text) =>
        RequireBridge(DocumentParseBridge.ReadPalettePath, "ReadPalettePath")(text);

    public static SqlDialect ReadSqlDialect(string text) =>
        RequireBridge(DocumentParseBridge.ReadSqlDialect, "ReadSqlDialect")(text);

    public static (string Id, string Title) ReadDashboardHeader(string text) =>
        RequireBridge(DocumentParseBridge.ReadDashboardHeader, "ReadDashboardHeader")(text);

    public static DashboardDocument Parse(string text, string? specDirectory = null) =>
        Parse(text, specDirectory, DashSpecParseOptions.Default);

    public static DashboardDocument Parse(
        string text,
        string? specDirectory,
        DashSpecParseOptions parseOptions) =>
        RequireBridge(DocumentParseBridge.Parse, "Parse")(text, specDirectory, parseOptions);

    private static Func<TArg, TResult> RequireBridge<TArg, TResult>(
        Func<TArg, TResult>? bridge,
        string operation) =>
        bridge ?? throw new InvalidOperationException(
            $"Document parse bridge not registered for {operation}. Reference DashSpec.Execution.Core.");

    private static Func<TArg1, TArg2, TArg3, TResult> RequireBridge<TArg1, TArg2, TArg3, TResult>(
        Func<TArg1, TArg2, TArg3, TResult>? bridge,
        string operation) =>
        bridge ?? throw new InvalidOperationException(
            $"Document parse bridge not registered for {operation}. Reference DashSpec.Execution.Core.");
}
