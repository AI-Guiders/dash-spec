

namespace DashSpec.Compiler.Contract;

/// <summary>Plugin-aware parse options for compile/bootstrap.</summary>
public interface IReportParseOptionsSource
{
    DashSpecParseOptions CreateOptions();
}
