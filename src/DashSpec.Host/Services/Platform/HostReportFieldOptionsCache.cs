using DashSpec.Core.Platform;
using DashSpec.Host.Services.Loading;

namespace DashSpec.Host.Services.Platform;

public sealed class HostReportFieldOptionsCache(IFieldOptionsCache inner) : IReportFieldOptionsCache
{
    public Task<IReadOnlyList<string>> GetOrLoadAsync(
        string cacheKey,
        Func<CancellationToken, Task<IReadOnlyList<string>>> loader,
        CancellationToken cancellationToken = default) =>
        inner.GetOrLoadAsync(cacheKey, loader, cancellationToken);
}
