using Microsoft.Extensions.Caching.Memory;

namespace DashSpec.Host.Services.Loading;

public interface IFieldOptionsCache
{
    Task<IReadOnlyList<string>> GetOrLoadAsync(
        string cacheKey,
        Func<CancellationToken, Task<IReadOnlyList<string>>> loader,
        CancellationToken cancellationToken = default);
}

public sealed class FieldOptionsCache(IMemoryCache cache, IConfiguration configuration) : IFieldOptionsCache
{
    public async Task<IReadOnlyList<string>> GetOrLoadAsync(
        string cacheKey,
        Func<CancellationToken, Task<IReadOnlyList<string>>> loader,
        CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue(cacheKey, out IReadOnlyList<string>? cached) && cached is not null)
        {
            return cached;
        }

        var values = await loader(cancellationToken).ConfigureAwait(false);
        var ttl = configuration.GetValue("DashSpec:FieldOptionsCacheMinutes", 15);
        cache.Set(
            cacheKey,
            values,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(Math.Max(1, ttl)),
            });

        return values;
    }
}
