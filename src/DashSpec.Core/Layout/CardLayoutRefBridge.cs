using DashSpec.Core.Parsing;

namespace DashSpec.Core.Layout;

/// <summary>Core bridge to F# <c>Modeling.Parse.Layout.CardLayoutRefResolver</c> — the SSOT for layout ref resolution.</summary>
internal static class CardLayoutRefBridge
{
    public static string Resolve(string token, IReadOnlyList<CardDefinition> cards, string context)
    {
        try
        {
            return Modeling.Parse.Layout.CardLayoutRefResolver.resolve(token, cards, context);
        }
        catch (DashSpec.Modeling.Core.DashSpecParseException ex)
        {
            throw new DashSpecParseException(ex.Message, ex.SourceOffset);
        }
    }
}
