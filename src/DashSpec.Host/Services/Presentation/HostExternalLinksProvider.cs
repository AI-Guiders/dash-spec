using System.Collections.Concurrent;
using DashSpec.Host.Configuration;

namespace DashSpec.Host.Services.Presentation;

public sealed record HostExternalLink(string Label, string Url, string Target, bool Topbar, bool Settings);

/// <summary>Resolves [[links]] from the active catalog entry @runtime TOML.</summary>
public sealed class HostExternalLinksProvider(DashSpecHostContext hostContext)
{
    private readonly ConcurrentDictionary<string, IReadOnlyList<HostExternalLink>> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<HostExternalLink> ForStartupRuntime() =>
        ForRuntimePath(hostContext.StartupRuntimeConfigPath);

    public IReadOnlyList<HostExternalLink> ForRuntimePath(string runtimeConfigPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeConfigPath);
        return _cache.GetOrAdd(runtimeConfigPath, static path => Load(path));
    }

    private static IReadOnlyList<HostExternalLink> Load(string runtimeConfigPath)
    {
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
