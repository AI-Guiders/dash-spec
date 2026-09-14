using DashSpec.Core.Model;



namespace DashSpec.Core.Parsing;



/// <summary>
/// Transitional parse entry in Core. Prefer Execution.Parsing.DashSpecParser for new code (ADR-0048 section 6).
/// Delegates to F# Modeling.Parse via DocumentParseBridge.
/// </summary>

public static class DashSpecParser

{

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



    internal static IReadOnlyList<Token> Tokenize(string text) => DashSpecLexer.Tokenize(text);



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

