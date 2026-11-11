using DashSpec.Abstractions.Connectors;
using Microsoft.Data.SqlClient;

namespace DashSpec.Publishing.SqlServer;

/// <summary>
/// The write face of the SqlServer acquisition plugin — the managed-view
/// publisher (ADR-0125 §2), not a data connector: it carries no query contract
/// and no rows, only DDL rights. CREATE OR ALTER each managed view, refresh
/// this project's leases (the reference counting that makes prune safe), and
/// drop dashspec_ objects with no other-project lease and no current revision.
/// The publishing connection string (per call) owns the rights; a read-only
/// deployment simply never calls this face.
/// </summary>
public sealed class SqlServerViewPublisher : IManagedViewPublisher
{
    public void Apply(IReadOnlyList<ManagedViewDdl> views, string connection, string projectUid)
    {
        using var conn = new SqlConnection(connection);
        conn.Open();

        using (var leaseTable = conn.CreateCommand())
        {
            leaseTable.CommandText = """
                IF OBJECT_ID('dashspec_view_leases') IS NULL
                CREATE TABLE dashspec_view_leases (
                    view_name NVARCHAR(256) NOT NULL,
                    project_uid NVARCHAR(128) NOT NULL,
                    updated_at_utc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT PK_dashspec_view_leases PRIMARY KEY (view_name, project_uid)
                )
                """;
            leaseTable.ExecuteNonQuery();
        }

        using (var clearOwn = conn.CreateCommand())
        {
            clearOwn.CommandText = "DELETE FROM dashspec_view_leases WHERE project_uid = @project;";
            clearOwn.Parameters.AddWithValue("@project", projectUid);
            clearOwn.ExecuteNonQuery();
        }

        foreach (var view in views)
        {
            using var ddl = conn.CreateCommand();
            ddl.CommandText = view.Ddl;
            ddl.ExecuteNonQuery();

            using var lease = conn.CreateCommand();
            lease.CommandText = """
                MERGE dashspec_view_leases AS target
                USING (SELECT @view AS view_name, @project AS project_uid) AS src
                ON target.view_name = src.view_name AND target.project_uid = src.project_uid
                WHEN MATCHED THEN UPDATE SET updated_at_utc = SYSUTCDATETIME()
                WHEN NOT MATCHED THEN INSERT (view_name, project_uid) VALUES (src.view_name, src.project_uid);
                """;
            lease.Parameters.AddWithValue("@view", view.ViewName);
            lease.Parameters.AddWithValue("@project", projectUid);
            lease.ExecuteNonQuery();
        }
    }

    public int PruneOrphans(string connection, string projectUid, IReadOnlyCollection<string> current)
    {
        using var conn = new SqlConnection(connection);
        conn.Open();

        var orphans = new List<(string Name, string Type)>();
        using (var find = conn.CreateCommand())
        {
            find.CommandText = """
                SELECT o.name, RTRIM(o.type)
                FROM sys.objects o
                WHERE o.name LIKE 'dashspec[_]%'
                  AND o.type IN ('V', 'IF', 'TF', 'FN')
                  AND NOT EXISTS (
                    SELECT 1 FROM dashspec_view_leases l
                    WHERE l.view_name = o.name AND l.project_uid <> @project
                  )
                """;
            find.Parameters.AddWithValue("@project", projectUid);
            using var reader = find.ExecuteReader();
            while (reader.Read())
            {
                var name = reader.GetString(0);
                var objType = reader.GetString(1);
                if (!current.Contains(name))
                {
                    orphans.Add((name, objType));
                }
            }
        }

        foreach (var (orphan, objType) in orphans)
        {
            var safeName = orphan.Replace("]", "]]");
            using var drop = conn.CreateCommand();
            drop.CommandText = objType == "V"
                ? $"DROP VIEW IF EXISTS [{safeName}]"
                : $"DROP FUNCTION IF EXISTS [{safeName}]";
            drop.ExecuteNonQuery();

            using var clearLease = conn.CreateCommand();
            clearLease.CommandText = "DELETE FROM dashspec_view_leases WHERE view_name = @view";
            clearLease.Parameters.AddWithValue("@view", orphan);
            clearLease.ExecuteNonQuery();
        }

        // stale lease rows for views that no longer exist (e.g. an interrupted run)
        using (var sweep = conn.CreateCommand())
        {
            sweep.CommandText = """
                DELETE l FROM dashspec_view_leases l
                WHERE NOT EXISTS (SELECT 1 FROM sys.views v WHERE v.name = l.view_name)
                  AND NOT EXISTS (SELECT 1 FROM sys.objects o WHERE o.name = l.view_name AND o.type IN ('IF', 'TF', 'FN'))
                """;
            sweep.ExecuteNonQuery();
        }

        return orphans.Count;
    }
}
