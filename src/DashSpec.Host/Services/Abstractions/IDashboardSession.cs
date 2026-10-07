using DashSpec.Core.Model;
using DashSpec.Core.Platform;
using DashSpec.Core.Runtime;
using DashSpec.Viz.Platform;

namespace DashSpec.Host.Services.Abstractions;

/// <summary>Blazor viewer session; implements platform <see cref="IReportSession"/> (ADR-0099).</summary>
public interface IDashboardSession : IReportSession
{
}
