namespace DashSpec.Core.Platform;

/// <summary>Distinct field-option list cache (viewer may provide memory-backed impl).</summary>
public interface IReportFieldOptionsCache
{
    Task<IReadOnlyList<string>> GetOrLoadAsync(
        string cacheKey,
        Func<CancellationToken, Task<IReadOnlyList<string>>> loader,
        CancellationToken cancellationToken = default);
}
