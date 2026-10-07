using DashSpec.Core.Parsing;

namespace DashSpec.Execution.Runtime.Platform;

/// <summary>Plugin-aware parse options for compile/bootstrap.</summary>
public interface IReportParseOptionsSource
{
    DashSpecParseOptions CreateOptions();
}
