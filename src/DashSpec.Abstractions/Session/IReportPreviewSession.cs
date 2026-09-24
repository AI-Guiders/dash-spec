namespace DashSpec.Abstractions.Session;

/// <summary>
/// Headless report preview session shared by Host viewer and Studio Report Preview.
/// Signature-only v0 — implementation lands with Execution.Runtime session extract.
/// </summary>
/// <seealso cref="design/DASHSPEC-II3-presentation-scaffold.md"/>
public interface IReportPreviewSession
{
    /// <summary>Load a resolved spec from disk (planet-relative or absolute path).</summary>
    Task LoadSpecAsync(string specPath, CancellationToken cancellationToken = default);

    /// <summary>Rebind filters and rebuild card payloads after session mutation.</summary>
    Task RefreshAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stable fingerprint of rendered payloads for Host vs Studio parity tests
    /// (<see href="design/DASHSPEC-ADR-0047-platform-surfaces-viewer-split.md">ADR-0047</see>).
    /// </summary>
    Task<string> GetPayloadFingerprintAsync(CancellationToken cancellationToken = default);
}
