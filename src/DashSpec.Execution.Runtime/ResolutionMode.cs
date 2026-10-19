namespace DashSpec.Execution.Runtime;

/// <summary>Host load context for display resolution (ADR-0057 §3).</summary>
public enum ResolutionMode
{
    /// <summary>Report opened through a catalog entry → tab module.</summary>
    CatalogProd,

    /// <summary><c>@dashboard</c> embed with <c>tab { dashspec … }</c>.</summary>
    DashboardEmbed,

    /// <summary>Direct `.ds` spec or dev upload without a catalog entry.</summary>
    SoakDev,
}
