using DashSpec.Execution.Runtime;
using DashSpec.Host.Services.Loading;

using DashSpec.Compiler.Contract;

namespace DashSpec.Host.Services.Platform;

public sealed class HostReportFieldOptionsCache(IFieldOptionsCache inner) : IReportFieldOptionsCache
{
    public Task<IReadOnlyList<string>> GetOrLoadAsync(
        string cacheKey,
        Func<CancellationToken, Task<IReadOnlyList<string>>> loader,
        CancellationToken cancellationToken = default) =>
        inner.GetOrLoadAsync(cacheKey, loader, cancellationToken);
}
