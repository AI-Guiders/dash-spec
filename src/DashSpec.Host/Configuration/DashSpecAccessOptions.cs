namespace DashSpec.Host.Configuration;

/// <summary>Host access gate — [access] api_key in dash-spec.toml or DASHSPEC_API_KEY env.</summary>
public sealed class DashSpecAccessOptions
{
    public const string HeaderName = "X-Api-Key";
    public const string CookieName = "dashspec-access";
    public const string QueryName = "api_key";

    public string ApiKey { get; set; } = string.Empty;

    public bool IsRequired => !string.IsNullOrWhiteSpace(ApiKey);
}
