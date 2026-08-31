#nullable enable

namespace DashSpec.Host.Commands;

/// <summary>
/// Ephemeral command-mode UI state: draft tail and derived highlight targets.
/// Isolated from page/data render tree (DASHSPEC-ADR-0043 surface layer).
/// </summary>
public sealed class DashboardCommandSession(DashboardFilterCommandService commandService)
{
    bool _barActive;
    bool _paletteActive;
    string _draftTail = "";
    CommandHighlightState _highlights = CommandHighlightState.Empty;

    public event Action? HighlightsChanged;

    public bool IsActive => _barActive || _paletteActive;

    public void SetBarActive(bool active, DashboardFilterContext context)
    {
        if (_barActive == active)
        {
            return;
        }

        _barActive = active;
        SyncHighlights(context);
    }

    public void SetPaletteActive(bool active, DashboardFilterContext context)
    {
        if (_paletteActive == active)
        {
            return;
        }

        _paletteActive = active;
        if (!active)
        {
            _draftTail = "";
        }

        SyncHighlights(context);
    }

    public void SetDraftTail(string tail, DashboardFilterContext context)
    {
        _draftTail = tail ?? "";
        if (!IsActive)
        {
            return;
        }

        ApplyHighlights(commandService.ResolveHighlights(_draftTail, context));
    }

    public void ClearDraft(DashboardFilterContext context)
    {
        _draftTail = "";
        if (!IsActive)
        {
            return;
        }

        ApplyHighlights(commandService.ResolveHighlights(_draftTail, context));
    }

    public bool IsFilterHighlighted(string filterName) =>
        IsActive && _highlights.FilterNames.Contains(filterName);

    public bool IsCardHighlighted(string cardId) =>
        IsActive && _highlights.CardIds.Contains(cardId);

    void SyncHighlights(DashboardFilterContext context)
    {
        if (!IsActive)
        {
            ApplyHighlights(CommandHighlightState.Empty);
            return;
        }

        ApplyHighlights(commandService.ResolveHighlights(_draftTail, context));
    }

    void ApplyHighlights(CommandHighlightState next)
    {
        if (HighlightEquals(_highlights, next))
        {
            return;
        }

        _highlights = next;
        HighlightsChanged?.Invoke();
    }

    static bool HighlightEquals(CommandHighlightState left, CommandHighlightState right) =>
        left.FilterNames.SetEquals(right.FilterNames)
        && left.CardIds.SetEquals(right.CardIds);
}

public enum CommandTargetKind
{
    Filter,
    Card,
}
