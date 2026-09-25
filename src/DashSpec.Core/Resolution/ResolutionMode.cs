namespace DashSpec.Core.Resolution;

/// <summary>Host load context for display resolution (ADR-0057 §3).</summary>
public enum ResolutionMode
{
    /// <summary>Report opened via <c>.dashcatalog</c> → <c>@tab</c> module.</summary>
    CatalogProd,

    /// <summary><c>@dashboard</c> embed with <c>tab { dashspec … }</c>.</summary>
    DashboardEmbed,

    /// <summary>Direct <c>.dashspec</c> / dev upload without catalog entry.</summary>
    SoakDev,
}
