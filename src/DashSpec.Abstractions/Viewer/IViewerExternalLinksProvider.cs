namespace DashSpec.Abstractions.Viewer;

public sealed record ViewerExternalLink(string Label, string Url, string Target, bool Topbar, bool Settings);

public interface IViewerExternalLinksProvider
{
    IReadOnlyList<ViewerExternalLink> ForStartupRuntime();
}
