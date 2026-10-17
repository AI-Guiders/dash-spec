// Ручная миграция: Host читает F# IR, не содержит собственной модели.
global using LayoutBoardDefinition = DashSpec.Modeling.Parse.Layout.LayoutBoardDefinition;
global using HostDocument = DashSpec.Modeling.Parse.Host.HostDocument;
global using LayoutBoardEntry = DashSpec.Modeling.Parse.Layout.LayoutBoardEntry;
global using LayoutScope = DashSpec.Modeling.Parse.Layout.LayoutScope;
global using LayoutDefinition = DashSpec.Modeling.Parse.Layout.LayoutDefinition;
global using PlacementDefinition = DashSpec.Modeling.Parse.Layout.PlacementDefinition;
global using FilterDefinition = DashSpec.Modeling.Parse.Filter.FilterDefinition;
global using FilterKind = DashSpec.Modeling.Parse.Filter.FilterKind;
global using DashboardDocument = DashSpec.Modeling.Parse.Document.DashboardDocument;
global using CardClickEffect = DashSpec.Modeling.Parse.Card.CardClickEffect;
global using ShowSelectionEffect = DashSpec.Modeling.Parse.Card.CardClickEffect;
global using InvokeHandlerEffect = DashSpec.Modeling.Parse.Card.CardClickEffect;
