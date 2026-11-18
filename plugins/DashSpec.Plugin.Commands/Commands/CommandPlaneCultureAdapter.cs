#nullable enable

using System.Globalization;
using AIGuiders.Platform.Execution.CommandPlane;
using DashSpec.Surface.Blazor.Commands;

namespace DashSpec.Plugin.Commands;

internal sealed class CommandPlaneCultureAdapter(IDashboardCultureAmbient inner) : ICultureAmbient
{
    public CultureInfo Culture => inner.Culture;
}
