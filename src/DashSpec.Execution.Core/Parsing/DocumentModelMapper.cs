using DashSpec.Core.Model;

namespace DashSpec.Execution.Parsing;

/// <summary>Maps F# Modeling.Parse document IR to Core model types (ADR-0048 M4).</summary>
internal static class DocumentModelMapper
{
    internal static DashboardDocument ToCore(DashSpec.Modeling.Parse.Document.DashboardDocument document) =>
        document;
}
