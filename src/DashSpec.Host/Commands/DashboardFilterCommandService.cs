#nullable enable
using AIGuiders.Platform.CommandPlane;
using DashSpec.Core.Model;
using DashSpec.Host.Services.Abstractions;

namespace DashSpec.Host.Commands;

public sealed class DashboardFilterCommandService(IDashboardSession session)
{
    public SlashCatalogIndex CurrentCatalog
    {
        get
        {
            try
            {
                var aliases = session.FilterIndex.Values
                    .Where(f => f.Kind != FilterKind.Date)
                    .Select(f => f.Name)
                    .Take(12);
                return DashboardFilterCommandCatalog.ForDocument(aliases);
            }
            catch (InvalidOperationException)
            {
                return DashboardFilterCommandCatalog.Bundled;
            }
        }
    }
}
