#nullable enable
using AIGuiders.Platform.CommandPlane.Commands;

namespace DashSpec.Host.Commands;



internal static class DashboardCommandRegistryFactory

{

    public static PlatformCommandRegistry<DashboardFilterContext> Create(

        DashboardFilterContext context,

        IEnumerable<IPlatformCommand<DashboardFilterContext>> pluginCommands)

    {

        var registry = new PlatformCommandRegistry<DashboardFilterContext>();

        registry.Register(new SelectReportCommand());

        registry.Register(new SelectPageCommand());

        registry.Register(new SelectDateFilterCommand());

        registry.Register(new SelectViewCommand());



        foreach (var filterName in context.ToolbarFilterNames

                     .Where(name => context.FilterIndex.TryGetValue(name, out var filter)

                                    && filter.Kind is DashSpec.Core.Model.FilterKind.Field)

                     .Distinct(StringComparer.OrdinalIgnoreCase))

        {

            registry.Register(new SelectFieldFilterCommand(filterName));

        }



        foreach (var command in pluginCommands)

        {

            registry.Register(command);

        }



        return registry;

    }

}

