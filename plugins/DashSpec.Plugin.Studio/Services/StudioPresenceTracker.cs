#nullable enable

using System.Collections.Concurrent;

namespace DashSpec.Plugin.Studio.Services;

public sealed record StudioPresenceEntry(
    string AuthorId,
    string AuthorName,
    string? ActiveRelativePath,
    IReadOnlySet<string> DraftPaths,
    DateTimeOffset UpdatedAtUtc);

/// <summary>
/// Singleton tracker showing which authors are currently viewing or editing files in <c>/studio</c> (ADR-0133 §3.2).
/// </summary>
public sealed class StudioPresenceTracker
{
    private readonly ConcurrentDictionary<string, StudioPresenceEntry> _entries = new(StringComparer.OrdinalIgnoreCase);

    public event Action? Changed;

    public void UpdateAuthor(
        string authorId,
        string authorName,
        string? activeRelativePath,
        IEnumerable<string> draftPaths)
    {
        var set = new HashSet<string>(draftPaths, StringComparer.OrdinalIgnoreCase);
        _entries[authorId] = new StudioPresenceEntry(
            authorId,
            authorName,
            activeRelativePath,
            set,
            DateTimeOffset.UtcNow);
        PruneStale();
        Changed?.Invoke();
    }

    public void RemoveAuthor(string authorId)
    {
        if (_entries.TryRemove(authorId, out _))
        {
            Changed?.Invoke();
        }
    }

    public IReadOnlyList<StudioPresenceEntry> GetActiveAuthors(string? excludeAuthorId = null)
    {
        PruneStale();
        return _entries.Values
            .Where(e => excludeAuthorId is null || !string.Equals(e.AuthorId, excludeAuthorId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.AuthorName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<StudioPresenceEntry> GetAuthorsOnFile(string relativePath, string? excludeAuthorId = null)
    {
        PruneStale();
        return _entries.Values
            .Where(e =>
                (excludeAuthorId is null || !string.Equals(e.AuthorId, excludeAuthorId, StringComparison.OrdinalIgnoreCase)) &&
                (string.Equals(e.ActiveRelativePath, relativePath, StringComparison.OrdinalIgnoreCase) ||
                 e.DraftPaths.Contains(relativePath)))
            .ToList();
    }

    private void PruneStale()
    {
        var cutoff = DateTimeOffset.UtcNow.AddHours(-4);
        foreach (var (key, value) in _entries)
        {
            if (value.UpdatedAtUtc < cutoff)
            {
                _entries.TryRemove(key, out _);
            }
        }
    }
}
