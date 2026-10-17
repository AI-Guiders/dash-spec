using DashSpec.Modeling.Parse.Filter;

namespace DashSpec.Filters;

public interface IFilterWidgetComponentResolver
{
    Type ResolveComponentType(FilterDefinition filter);
}
