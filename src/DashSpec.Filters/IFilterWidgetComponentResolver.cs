using DashSpec.Core.Model;

namespace DashSpec.Filters;

public interface IFilterWidgetComponentResolver
{
    Type ResolveComponentType(FilterDefinition filter);
}
