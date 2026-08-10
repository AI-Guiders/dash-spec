using DashSpec.Abstractions.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DashSpec.Host.Plugins.Builtins;

public sealed class DiagramBuiltinPlugin : IDashSpecPlugin
{
    public string Id => "diagram_builtin";

    public string DisplayName => "Built-in diagram kinds";

    public PluginTier Tier => PluginTier.Core;

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
    }

    public void RegisterContributors(IDashSpecContributorRegistry registry)
    {
        RegisterKind(registry, Id, "line", "Chart", ["x", "y", "series", "reference"]);
        RegisterKind(registry, Id, "area", "Chart", ["x", "y", "series", "reference"]);
        RegisterKind(registry, Id, "sparkline", "Chart", ["x", "y", "series"]);
        RegisterKind(registry, Id, "bar", "Chart", ["x", "y", "series", "reference", "category", "value"]);
        RegisterKind(registry, Id, "pie", "Chart", ["x", "y", "series", "category", "value"], supportsTopLimit: true);
        RegisterKind(registry, Id, "donut", "Chart", ["x", "y", "series", "category", "value"], supportsTopLimit: true);
        RegisterKind(registry, Id, "doughnut", "Chart", ["x", "y", "series", "category", "value"], supportsTopLimit: true);
        RegisterKind(registry, Id, "scatter", "Chart", ["x", "y", "size"]);
        RegisterKind(registry, Id, "histogram", "Chart", ["value", "x", "bins", "bin_width"]);
        RegisterKind(registry, Id, "box", "Chart", ["value", "x"]);
        RegisterKind(registry, Id, "boxplot", "Chart", ["value", "x"]);
        RegisterKind(registry, Id, "treemap", "Chart", ["x", "y", "category", "value"], supportsTopLimit: true);
        RegisterKind(registry, Id, "gauge", "Chart", ["value", "aggregate", "min", "max"]);
        RegisterKind(registry, Id, "table", "Table", ["columns", "order_by", "limit"], supportsTopLimit: true);
        RegisterKind(registry, Id, "number", "Scalar", ["value", "aggregate", "scale_value", "delta"]);
        RegisterKind(registry, Id, "heatmap", "Matrix", ["x", "y", "value", "tooltip"]);
    }

    private static void RegisterKind(
        IDashSpecContributorRegistry registry,
        string pluginId,
        string kindId,
        string family,
        IReadOnlyList<string> bindings,
        bool supportsTopLimit = false)
    {
        registry.AddDiagramKind(new DiagramKindContributorDescriptor(
            pluginId,
            kindId,
            family,
            supportsTopLimit,
            bindings));
    }
}
