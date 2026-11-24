

namespace DashSpec.Compiler.Contract;

/// <summary>Platform compile &amp; language front-end port — wraps F# <c>DashSpecCompiler</c> and unit parsers (ADR-0099 B4).</summary>
public interface IReportCompiler
{
    ReportCompileResult Compile(string text, string? specDirectory, DashSpecParseOptions parseOptions);

    ReportCompileResult Compile(string text, string? specDirectory);

    string? ReadRuntimePath(string text);

    HostDocument ParseHost(string text, string? specDirectory = null);

    HostDocument ParseHostFile(string path);

    CatalogDocument ParseCatalog(string text);

    CatalogDocument ParseCatalogFile(string path);

    RuntimeDocument ParseRuntime(string text);

    LayoutBoardDefinition ParseLayout(string text);

    string? TryDiscoverHostSpecPath(string directory, bool skipLocalConfig = false);

    string ReadEmbeddedUnit(string logicalPath);
}
