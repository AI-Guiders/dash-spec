namespace DashSpec.Host.Configuration;

public sealed class DashSpecTomlRoot
{
    public DashboardTomlSection Dashboard { get; set; } = new();

    public HostTomlSection Host { get; set; } = new();

    public CatalogGitTomlSection CatalogGit { get; set; } = new();

    public AccessTomlSection Access { get; set; } = new();
    public PresentationTomlSection Presentation { get; set; } = new();

    public ReportTimeTomlSection ReportTime { get; set; } = new();

    /// <summary>
    /// Visual layer ([ADR-0111] visual pilot, operator 2026-10-22): the renderer is a host
    /// decision, not a spec one — picked once here per data family, diagrams carry no
    /// <c>render =</c>. Falls back to the built-in family default when empty.
    /// </summary>
    public VizTomlSection Viz { get; set; } = new();

    public Dictionary<string, ProviderTomlSection> Providers { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public PluginsTomlSection Plugins { get; set; } = new();

    /// <summary>Legacy TOML [[links]] — rejected at load; links live in <c>.dashhost</c>.</summary>
    public List<ExternalLinkTomlEntry> Links { get; set; } = [];
}

public sealed class ExternalLinkTomlEntry
{
    public string Id { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    /// <summary>HTML target; empty → _blank.</summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>Show in top bar near Dashboard. Default true when omitted.</summary>
    public bool? Topbar { get; set; }

    /// <summary>Show in Settings → Links. Default true when omitted.</summary>
    public bool? Settings { get; set; }
}

public sealed class HostTomlSection
{
    /// <summary>Planet-content shell module (<c>.dashhost</c>, ADR-0054).</summary>
    public string Dashhost { get; set; } = string.Empty;

    /// <summary>WitDB path for Control Center settings (ADR-0042). Default: %ProgramData%\DashSpec\host-settings.witdb</summary>
    public string DatabasePath { get; set; } = string.Empty;

    /// <summary>Windows service name for deployment (optional; default <c>DashSpecHost</c>).</summary>
    public string ServiceName { get; set; } = string.Empty;
}

public sealed class PresentationTomlSection
{
    /// <summary>Display time zone for report/table output (remark 25). IANA id; default Europe/Moscow.</summary>
    public string DisplayTimeZone { get; set; } = string.Empty;

    /// <summary>UI color scheme (remark 24): dark default | light | contrast. Empty = dark.</summary>
    public string ColorScheme { get; set; } = string.Empty;

    /// <summary>UI culture (remark 23): BCL specific name (<c>ru-RU</c>, <c>en-US</c>, …). Empty in TOML → cold-start <see cref="DashSpecCultures.BootstrapDefaultName"/> at host startup.</summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>Long field filter layout (remark 7): scroll | expand. Empty = scroll.</summary>
    public string LargeFieldFilterLayout { get; set; } = string.Empty;
}

public sealed class AccessTomlSection
{
    public string ApiKey { get; set; } = string.Empty;
}

/// <summary>
/// Renderer ids per data family ([ADR-0111] visual pilot): the host picks the visual layer
/// once — spec diagrams never say who renders them. Empty keys keep the built-in defaults.
/// </summary>
public sealed class VizTomlSection
{
    /// <summary>Chart family (line, bar, pie, donut, scatter, area). Empty = chartjs.</summary>
    public string Chart { get; set; } = string.Empty;

    /// <summary>Table family. Empty = table-html.</summary>
    public string Table { get; set; } = string.Empty;

    /// <summary>Matrix family (heatmap). Empty = matrix-canvas.</summary>
    public string Matrix { get; set; } = string.Empty;

    /// <summary>Gantt family. Empty = gantt-html.</summary>
    public string Gantt { get; set; } = string.Empty;

    /// <summary>Scalar family (numbers). Empty = scalar-html.</summary>
    public string Scalar { get; set; } = string.Empty;
}

public sealed class DashboardTomlSection
{
    public string CatalogPath { get; set; } = string.Empty;
}

/// <summary>Опциональный git-источник каталога (.dashcatalog + specs). Перекрывает <see cref="DashboardTomlSection.CatalogPath"/>.</summary>
public sealed class CatalogGitTomlSection
{
    public bool Enabled { get; set; }

    public string Url { get; set; } = string.Empty;

    public string Branch { get; set; } = "main";

    /// <summary>Путь к .dashcatalog внутри репозитория.</summary>
    public string Path { get; set; } = string.Empty;

    public int PullIntervalMinutes { get; set; } = 15;

    public string CacheDirectory { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    /// <summary>HMAC secret for POST /v1/admin/catalog/sync (DASHSPEC-ADR-0041).</summary>
    public string SyncWebhookSecret { get; set; } = string.Empty;

    /// <summary>Lab only: accept unsigned sync POSTs when secret is set.</summary>
    public bool SyncAllowUnsigned { get; set; }

    /// <summary>Optional filter vs Forge payload repo / refs.repo.</summary>
    public string SyncRepoSlug { get; set; } = string.Empty;
}

public sealed class ProviderTomlSection
{
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>SqlCommand.CommandTimeout (seconds). 0 = connector default (120).</summary>
    public int CommandTimeoutSeconds { get; set; }

    /// <summary>Abort when result row count exceeds limit (0 = connector default 250000).</summary>
    public int MaxRows { get; set; }
}

public sealed class PluginsTomlSection
{
    public string DefaultProviderId { get; set; } = "sqlserver";

    public string ActiveBundle { get; set; } = "standard";

    public List<PluginBundleTomlEntry> Bundles { get; set; } = [];

    public List<PluginLoadTomlEntry> Load { get; set; } = [];
}

public sealed class PluginBundleTomlEntry
{
    public string Name { get; set; } = string.Empty;

    public List<string> Plugins { get; set; } = [];
}

public sealed class PluginLoadTomlEntry
{
    public string Id { get; set; } = string.Empty;

    public string Assembly { get; set; } = string.Empty;

    public string Tier { get; set; } = "extended";

    public bool IsProvider { get; set; }
}
