using Microsoft.FSharp.Reflection;

namespace DashSpec.Core.Tests;

internal static class CardClickEffectTestData
{
    private static readonly Type UnionType = typeof(DashSpec.Modeling.Parse.Card.CardClickEffect);

    public static string CaseName(DashSpec.Modeling.Parse.Card.CardClickEffect effect) =>
        FSharpValue.GetUnionFields(effect, UnionType, null).Item1.Name;

    public static object?[] Fields(DashSpec.Modeling.Parse.Card.CardClickEffect effect) =>
        FSharpValue.GetUnionFields(effect, UnionType, null).Item2;
}
