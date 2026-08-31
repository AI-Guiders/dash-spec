#nullable enable
using AIGuiders.Platform.CommandPlane;
using DashSpec.Core.Model;
using DashSpec.Host.Services.Abstractions;

namespace DashSpec.Host.Commands;

public sealed class DashboardFilterPickerSource(
    IDashboardSession session,
    IReadOnlyList<string> toolbarFilterNames) : ICommandPickerChoiceSource
{
    public IReadOnlyList<CommandPickerChoice> GetChoices(string pickerId, string partial)
    {
        if (!pickerId.StartsWith("dash.field.", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        var slashAlias = pickerId["dash.field.".Length..];
        var filterName = DashboardCommandAliasResolver.ResolveFieldFilter(
            slashAlias,
            CreateContext());
        if (filterName is null)
        {
            return [];
        }

        var options = session.GetFieldOptions(filterName);
        return options
            .Where(option => Matches(option, partial))
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
            FilterIndex = session.FilterIndex,
            ToolbarFilterNames = toolbarFilterNames,
            CommandAliases = session.Document.ResolvedCommandAliases,
            UiState = new Services.Presentation.DashboardFilterUiState(),
            GetFieldOptions = session.GetFieldOptions,
        };
}
