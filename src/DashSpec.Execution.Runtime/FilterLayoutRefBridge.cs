using DashSpec.Core.Parsing;

namespace DashSpec.Execution.Runtime;

/// <summary>Core bridge to F# <c>Modeling.Parse.Filter.FilterLayoutRefResolver</c> — translates the F# parse exception to the Core one.</summary>
internal static class FilterLayoutRefBridge
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
