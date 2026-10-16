// Алиасы F#-модели (ADR-0048 / ADR-0103): в C# своей модели нет.
// global using-алиас перекрывает одноимённый тип из `DashSpec.Core.Model`,
// поэтому потребители видят F#-IR, а C#-тип удаляется.
global using LayoutDefinition = DashSpec.Modeling.Parse.Layout.LayoutDefinition;
global using PlacementDefinition = DashSpec.Modeling.Parse.Layout.PlacementDefinition;
global using LayoutBoardNestDefinition = DashSpec.Modeling.Parse.Layout.LayoutBoardNestDefinition;
global using PaletteDefinition = DashSpec.Modeling.Parse.Palette.PaletteDocument;
global using CardBoundFilterChrome = DashSpec.Modeling.Parse.Card.CardBoundFilterChrome;
global using CardFoldMode = DashSpec.Modeling.Parse.Card.CardFoldMode;
global using CardChromeDefinition = DashSpec.Modeling.Parse.Card.CardChromeDefinition;
global using CardsFoldPolicy = DashSpec.Modeling.Parse.Card.CardsFoldPolicy;
global using CardsChromeDefinition = DashSpec.Modeling.Parse.Card.CardsChromeDefinition;
global using TooltipDefinition = DashSpec.Modeling.Parse.Tooltip.TooltipDefinition;
