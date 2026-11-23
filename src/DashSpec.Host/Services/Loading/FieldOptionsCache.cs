using System.Collections.Concurrent;
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
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _keyLocks = new(StringComparer.Ordinal);

    public async Task<IReadOnlyList<string>> GetOrLoadAsync(
        string cacheKey,
        Func<CancellationToken, Task<IReadOnlyList<string>>> loader,
        CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue(cacheKey, out IReadOnlyList<string>? cached) && cached is not null)
        {
            return cached;
        }

        var gate = _keyLocks.GetOrAdd(cacheKey, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (cache.TryGetValue(cacheKey, out cached) && cached is not null)
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
        finally
        {
            gate.Release();
        }
    }
}
