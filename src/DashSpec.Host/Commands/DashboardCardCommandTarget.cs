#nullable enable

namespace DashSpec.Host.Commands;

public sealed record DashboardCardViewOption(string ViewId, string Label);

public sealed record DashboardCardCommandTarget(
    string CardId,
    string Title,
    IReadOnlyList<DashboardCardViewOption> Views);
