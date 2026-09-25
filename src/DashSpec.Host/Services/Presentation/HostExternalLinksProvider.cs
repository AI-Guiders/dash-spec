using DashSpec.Host.Configuration;

namespace DashSpec.Host.Services.Presentation;

public sealed record HostExternalLink(string Label, string Url, string Target, bool Topbar, bool Settings);

/// <summary>External topbar links from <c>.dashhost</c>.</summary>
public sealed class HostExternalLinksProvider(DashSpecHostContext hostContext)
{
    public IReadOnlyList<HostExternalLink> ForStartupRuntime() => Load(hostContext.HostShell);

    public IReadOnlyList<HostExternalLink> ForRuntimePath(string runtimeConfigPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeConfigPath);
        return Load(hostContext.HostShell);
    }

    private static IReadOnlyList<HostExternalLink> Load(HostShellBootstrap hostShell) =>
        hostShell.Document.Links
            .Select(link => new HostExternalLink(
                link.Label.Trim(),
                link.Url.Trim(),
                string.IsNullOrWhiteSpace(link.Target) ? "_blank" : link.Target.Trim(),
                link.Topbar,
                link.Settings))
            .ToList();
}
