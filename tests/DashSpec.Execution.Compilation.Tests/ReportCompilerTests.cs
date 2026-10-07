using DashSpec.Core.Platform;
using DashSpec.Execution.Compilation;
using ExecutionDashSpecParser = DashSpec.Execution.Parsing.DashSpecParser;
using Xunit;

namespace DashSpec.Execution.Compilation.Tests;

public sealed class ReportCompilerTests
{
    public ReportCompilerTests() => ExecutionDashSpecParser.EnsureModuleParsersRegistered();

    [Fact]
    public void ReportCompiler_implements_IReportCompiler()
    {
        IReportCompiler compiler = new ReportCompiler();
        Assert.NotNull(compiler);
    }
}
