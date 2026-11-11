namespace DashSpec.Abstractions.Connectors;

/// <summary>One managed view to publish (ADR-0125): the deterministic
/// content-addressed name and the dialect DDL. The Acquisition-side contract
/// carries text, not the Compilation record — Abstractions must not depend on
/// the compiler.</summary>
public sealed record ManagedViewDdl(string ViewName, string Ddl);

/// <summary>
/// The managed-view publisher (ADR-0125 §2) — the write face of the acquisition
/// layer, not a connector: a connector's contract is «bring report rows» (read);
/// the publisher's is «my connection owns the DDL rights» (write). Separate
/// capability by design: a read-only deployment ships connectors and never a
/// publisher, and the publishing connection string is passed per call.
/// </summary>
public interface IManagedViewPublisher
{
    /// <summary>CREATE OR ALTER each view and refresh this project's leases
    /// (the reference counting that makes prune safe).</summary>
    void Apply(IReadOnlyList<ManagedViewDdl> views, string connection, string projectUid);

    /// <summary>Drop dashspec_ objects with no other-project lease and no current
    /// revision. Returns the number of dropped orphans.</summary>
    int PruneOrphans(string connection, string projectUid, IReadOnlyCollection<string> current);
}
