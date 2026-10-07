using System.Globalization;
using DashSpec.Core.Catalog;
using DashSpec.Host.Services.Abstractions;
using DashSpec.Host.Security;

namespace DashSpec.Host.Configuration;

/// <summary>Cold-start planet context shared across Host DI (ADR-0099 B3.4).</summary>
public sealed class DashSpecPlanetStartup
{
    public required DashSpecTomlRoot Bootstrap { get; init; }

    public required HostShellBootstrap HostShell { get; init; }

    public required CatalogBootstrap Catalog { get; init; }

    public required CatalogSourceState CatalogState { get; init; }

    public required string DefaultSpecPath { get; init; }

    public required string StartupConfigPath { get; init; }

    public required string StartupRuntimeReference { get; init; }

    public required string DefaultSpecRelativePath { get; init; }

    public required string DefaultSpecDirectory { get; init; }

    public required CultureInfo UiCulture { get; init; }

    public required TimeZoneInfo DisplayTimeZone { get; init; }

    public required DashSpecAccessOptions AccessOptions { get; init; }

    public required string HostDatabasePath { get; init; }

    public required IHostDatabaseInitializer HostDatabase { get; init; }
}
