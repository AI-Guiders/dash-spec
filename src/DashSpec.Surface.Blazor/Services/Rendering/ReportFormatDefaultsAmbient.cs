using DashSpec.Core.Model;
using DashSpec.Execution.Runtime;

namespace DashSpec.Surface.Blazor.Services.Rendering;

/// <summary>Single Host entry point for report-level format defaults (ADR-0074 / DSCHOST002).</summary>
public sealed class ReportFormatDefaultsAmbient
{
    public void Apply(ReportFormatDefaults? defaults) =>
        LabelFormat.SetReportDefaults(defaults);

    public void Clear() =>
        LabelFormat.ClearReportDefaults();
}
