using DashSpec.Core.Model;
using DashSpec.Core.Parsing;

namespace DashSpec.Core.Layout;

/// <summary>SSOT: <c>DashSpec.Modeling.Parse.Filter.FilterLayoutRefResolver</c> (F#).</summary>
internal static class FilterLayoutRefResolver
{
    public static string Resolve(string token, IReadOnlyList<FilterDefinition> filters, string context)
    {
        try
        {
            return Modeling.Parse.Filter.FilterLayoutRefResolver.resolve(
                token,
                filters,
                context);
        }
        catch (DashSpec.Modeling.Core.DashSpecParseException ex)
        {
            throw new DashSpecParseException(ex.Message, ex.SourceOffset);
        }
    }

}
