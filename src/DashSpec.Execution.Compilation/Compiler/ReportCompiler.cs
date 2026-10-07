using DashSpec.Core.Parsing;
using DashSpec.Core.Platform;
using ExecutionDashSpecParser = DashSpec.Execution.Parsing.DashSpecParser;

namespace DashSpec.Execution.Compilation;

/// <summary>C# adapter over F# <c>DashSpecCompiler.compile</c> (ADR-0099 B4).</summary>
public sealed class ReportCompiler : IReportCompiler
{
    public ReportCompileResult Compile(string text, string? specDirectory, DashSpecParseOptions parseOptions) =>
        ExecutionDashSpecParser.Compile(text, specDirectory, parseOptions);

    public ReportCompileResult Compile(string text, string? specDirectory) =>
        ExecutionDashSpecParser.Compile(text, specDirectory);
}
