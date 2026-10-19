

namespace DashSpec.Compiler.Contract;

/// <summary>Platform compile port — wraps F# <c>DashSpecCompiler</c> (ADR-0099 B4).</summary>
public interface IReportCompiler
{
    ReportCompileResult Compile(string text, string? specDirectory, DashSpecParseOptions parseOptions);

    ReportCompileResult Compile(string text, string? specDirectory);
}
