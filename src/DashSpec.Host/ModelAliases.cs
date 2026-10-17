// Ручная миграция: Host читает F# IR, не содержит собственной модели.
global using LayoutBoardDefinition = DashSpec.Modeling.Parse.Layout.LayoutBoardDefinition;
global using HostDocument = DashSpec.Modeling.Parse.Host.HostDocument;
global using FilterKind = DashSpec.Modeling.Parse.Filter.FilterKind;
global using DashboardDocument = DashSpec.Modeling.Parse.Document.DashboardDocument;
global using CardClickEffect = DashSpec.Modeling.Parse.Card.CardClickEffect;
global using ShowSelectionEffect = DashSpec.Modeling.Parse.Card.CardClickEffect;
