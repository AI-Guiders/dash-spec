#nullable enable
using AIGuiders.Platform.CommandPlane;
using AIGuiders.Platform.CommandPlane.ArgSuggestions;
using DashSpec.Core.Model;
using DashSpec.Host.Services.Abstractions;

namespace DashSpec.Host.Commands;

public sealed class DashboardFilterSuggestionProvider(
    IDashboardSession session,
    IReadOnlyList<string> toolbarFilterNames) : IArgSuggestionProvider
{
    public IReadOnlyList<CommandPickerChoice> GetSuggestions(ArgSuggestionRequest request)
    {
        if (!request.SuggestionId.StartsWith("dash.field.", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        var slashAlias = request.SuggestionId["dash.field.".Length..];
        var filterName = DashboardCommandAliasResolver.ResolveFieldFilter(
            slashAlias,
            CreateContext());
        if (filterName is null)
        {
            return [];
        }

        var options = session.GetFieldOptions(filterName);
        return options
            .Where(option => Matches(option, request.Partial))
            .Select(option => new CommandPickerChoice { Value = option, Label = option })
            .ToList();
    }

    static bool Matches(string option, string partial)
    {
        if (string.IsNullOrWhiteSpace(partial))
        {
            return true;
        }

        return option.Contains(partial, StringComparison.OrdinalIgnoreCase);
    }

    DashboardFilterContext CreateContext() =>
        new()
        {
            ReportId = session.Document.Id,
            ActiveScope = [DashSpecCommandScope.Dashboard],
            FilterIndex = session.FilterIndex,
            ToolbarFilterNames = toolbarFilterNames,
            CommandAliases = session.Document.ResolvedCommandAliases,
            UiState = new Services.Presentation.DashboardFilterUiState(),
            GetFieldOptions = session.GetFieldOptions,
        };
}
