namespace DashSpec.Viz;

public interface ICardVizComponentResolver
{
    Type? TryGetComponentType(string renderPluginId);
}
