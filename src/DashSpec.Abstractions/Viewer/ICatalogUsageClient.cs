using Microsoft.AspNetCore.Http;

namespace DashSpec.Abstractions.Viewer;

/// <summary>Per-client catalog entry preferences (remark 31). Host implements with EF.</summary>
public interface ICatalogUsageClient
{
    string GetOrCreateClientId(HttpContext? httpContext);

    string? ResolvePreferredEntryId(string clientId, string catalogDefaultEntryId);

    void RecordSelection(string clientId, string entryId);
}
