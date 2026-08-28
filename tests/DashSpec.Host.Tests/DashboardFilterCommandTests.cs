using AIGuiders.Platform.CommandPlane;
using DashSpec.Core.Model;
using DashSpec.Core.Runtime;
using DashSpec.Host.Commands;
using DashSpec.Host.Services.Presentation;
using Xunit;

namespace DashSpec.Host.Tests;

public class DashboardFilterCommandTests
{
    [Theory]
    [InlineData("today")]
    [InlineData("last-week")]
    [InlineData("2026-07")]
    [InlineData("2026-07-01..2026-07-15")]
    public void SelectDateFilterCommand_applies_range(string argTail)
    {
        var uiState = new DashboardFilterUiState();
        var context = CreateContext(uiState, ["usage_date"]);
        context.ArgTail = argTail;

        var command = new SelectDateFilterCommand();
        var outcome = command.ExecuteAsync(context).AsTask().GetAwaiter().GetResult();

        Assert.True(outcome.Success, outcome.Error);
        Assert.True(uiState.DateFrom.ContainsKey("usage_date"));
        Assert.True(uiState.DateTo.ContainsKey("usage_date"));
    }

    [Fact]
    public void SelectFieldFilterCommand_resolves_alias_and_single_value()
    {
        var uiState = new DashboardFilterUiState();
        var context = CreateContext(
            uiState,
            ["app_name"],
            aliases: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["app"] = "app_name",
            },
            options: new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["app_name"] = ["AutoCAD", "Revit"],
            });
        context.ArgTail = "AutoCAD";

        var command = new SelectFieldFilterCommand("app");
        var outcome = command.ExecuteAsync(context).AsTask().GetAwaiter().GetResult();

        Assert.True(outcome.Success, outcome.Error);
        Assert.Equal(["AutoCAD"], uiState.SelectedFields["app_name"]);
    }

    [Fact]
    public void Executor_runs_slash_line_through_registry()
    {
        var uiState = new DashboardFilterUiState();
        var context = CreateContext(uiState, ["usage_date"]);
        var catalog = DashboardCommandCatalogBuilder.Build(context, []);
        var executor = new DashboardCommandExecutor(new DashSpecCommandPluginRegistry());

        var outcome = executor.TryExecuteSlashLine("/select date today", context, catalog);

        Assert.True(outcome.Success, outcome.Error);
        Assert.Equal(new DateOnly(2026, 6, 24), uiState.DateFrom["usage_date"]);
        Assert.Equal(new DateOnly(2026, 6, 24), uiState.DateTo["usage_date"]);
    }

    static DashboardFilterContext CreateContext(
        DashboardFilterUiState uiState,
        IReadOnlyList<string> toolbarFilters,
        IReadOnlyDictionary<string, string>? aliases = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? options = null)
    {
        var filterIndex = new Dictionary<string, FilterDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["usage_date"] = new(
                FilterKind.Date,
                "usage_date",
                "-7d..today",
                "usage_date"),
            ["app_name"] = new(
                FilterKind.Field,
                "app_name",
                null,
                "app_name",
                Widget: "chips"),
        };

        return new DashboardFilterContext
        {
            ReportId = "demo",
            FilterIndex = filterIndex,
            ToolbarFilterNames = toolbarFilters,
            CommandAliases = aliases ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            UiState = uiState,
            GetFieldOptions = name => options?.TryGetValue(name, out var values) == true ? values : [],
            TodayUtc = new DateOnly(2026, 6, 24),
        };
    }
}
