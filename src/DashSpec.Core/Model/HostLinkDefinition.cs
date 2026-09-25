namespace DashSpec.Core.Model;

public sealed record HostLinkDefinition(
    string Id,
    string Label,
    string Url,
    string Target,
    bool Topbar,
    bool Settings);
