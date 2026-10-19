// ADR-0048: DiagramDataFamily is nested in the F# DiagramKindRegistry module;
// the alias de-nests it for C# consumers. Flat IR aliases were removed.
global using DiagramDataFamily = DashSpec.Modeling.Parse.Diagram.DiagramKindRegistry.DiagramDataFamily;
