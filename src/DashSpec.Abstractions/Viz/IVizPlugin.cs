namespace DashSpec.Abstractions.Viz;

/// <summary>Host viz backend (Chart.js, CSS grid, …). External DLLs implement this contract.</summary>
public interface IVizPlugin
{
    string Id { get; }

    /// <summary>Default data family when <c>render</c> is omitted (chart, table, scalar, matrix).</summary>
    string DataFamily { get; }
}
