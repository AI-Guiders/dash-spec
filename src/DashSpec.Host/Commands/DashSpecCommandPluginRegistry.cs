#nullable enable
using AIGuiders.Platform.CommandPlane.Commands;

namespace DashSpec.Host.Commands;

/// <summary>Plugin-provided dashboard commands collected at host startup (ADR-0043 W4).</summary>
public sealed class DashSpecCommandPluginRegistry
{
    private readonly List<IPlatformCommand<DashboardFilterContext>> _commands = [];

    public IReadOnlyList<IPlatformCommand<DashboardFilterContext>> Commands => _commands;

    public void Register(IPlatformCommand<DashboardFilterContext> command)
    {
        ArgumentNullException.ThrowIfNull(command);
        _commands.Add(command);
    }
}

/// <summary>Optional hook for plugins that ship executable dashboard commands.</summary>
public interface IDashSpecCommandPlugin
{
    void RegisterCommands(DashSpecCommandPluginRegistry registry);
}
