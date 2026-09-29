namespace DashSpec.Core.Model;

public enum CardsFoldPolicy
{
    None = 0,
    FocusSingle = 1,
}

public sealed record CardsChromeDefinition(CardsFoldPolicy FoldPolicy = CardsFoldPolicy.None)
{
    public static CardsChromeDefinition Default { get; } = new();
}
