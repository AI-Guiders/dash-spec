using DashSpec.Abstractions.Query;

namespace DashSpec.Execution.Compilation.Dialects;

internal static class SqlDialectBackendBootstrap
{
    private static int _registered;

    internal static void EnsureRegistered()
    {
        if (Interlocked.Exchange(ref _registered, 1) == 1)
        {
            return;
        }

        SqlDialectRegistry.Register(new TSqlDialectBackend());
        SqlDialectRegistry.Register(new PostgresDialectBackend());
        SqlDialectRegistry.Register(new GenericDialectBackend());
    }
}
