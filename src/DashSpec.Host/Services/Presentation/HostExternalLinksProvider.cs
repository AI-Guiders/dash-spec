using System.Collections.Concurrent;
using DashSpec.Host.Configuration;

namespace DashSpec.Host.Services.Presentation;

public sealed record HostExternalLink(string Label, string Url, string Target, bool Topbar, bool Settings);

/// <summary>External topbar links from <c>.dashhost</c> with runtime TOML fallback.</summary>
public sealed class HostExternalLinksProvider(DashSpecHostContext hostContext)
{
    private readonly ConcurrentDictionary<string, IReadOnlyList<HostExternalLink>> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<HostExternalLink> ForStartupRuntime() =>
        ForRuntimePath(hostContext.StartupRuntimeConfigPath);

    public IReadOnlyList<HostExternalLink> ForRuntimePath(string runtimeConfigPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeConfigPath);
        return _cache.GetOrAdd(runtimeConfigPath, path => Load(path, hostContext.HostShell));
    }

    private static IReadOnlyList<HostExternalLink> Load(string runtimeConfigPath, HostShellBootstrap? hostShell)
    {
        if (hostShell?.Document.Links.Count > 0)
        {
            return hostShell.Document.Links
                .Select(link => new HostExternalLink(
                    link.Label.Trim(),
                    link.Url.Trim(),
                    string.IsNullOrWhiteSpace(link.Target) ? "_blank" : link.Target.Trim(),
                    link.Topbar,
                    link.Settings))
                .ToList();
        }

        var root = DashSpecTomlLoader.LoadFile(runtimeConfigPath);
        var links = new List<HostExternalLink>();
        foreach (var entry in root.Links)
        {
            if (string.IsNullOrWhiteSpace(entry.Label) || string.IsNullOrWhiteSpace(entry.Url))
            {
                continue;
            }

            links.Add(new HostExternalLink(
                entry.Label.Trim(),
                entry.Url.Trim(),
                string.IsNullOrWhiteSpace(entry.Target) ? "_blank" : entry.Target.Trim(),
                entry.Topbar ?? true,
                entry.Settings ?? true));
        }

        return links;
    }
}
