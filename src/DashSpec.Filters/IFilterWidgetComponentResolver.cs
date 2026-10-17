
namespace DashSpec.Filters;

public interface IFilterWidgetComponentResolver
{
    Type ResolveComponentType(FilterDefinition filter);
}
