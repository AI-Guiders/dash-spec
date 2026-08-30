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
        context.CanonicalPath = FilterCommandPaths.FilterPath("usage_date");
        context.ArgTail = argTail;

        var command = new SelectDateFilterCommand();
        var outcome = command.ExecuteAsync(context).AsTask().GetAwaiter().GetResult();

        Assert.True(outcome.Success, outcome.Error);
        Assert.True(uiState.DateFrom.ContainsKey("usage_date"));
        Assert.True(uiState.DateTo.ContainsKey("usage_date"));
    }

    [Fact]
    public void ToInputTail_strips_fixed_select_prefix()
    {
        Assert.Equal("", DashboardFilterSlashCompletion.ToInputTail(""));
        Assert.Equal("filter usage_date", DashboardFilterSlashCompletion.ToInputTail("select filter usage_date"));
        Assert.Equal("filter", DashboardFilterSlashCompletion.ToInputTail("> select filter"));
    }

    [Theory]
    [InlineData("Tab", false, false, false, true)]
    [InlineData(" ", true, false, false, true)]
    [InlineData(" ", false, false, false, false)]
    [InlineData("Enter", false, false, false, false)]
    public void AcceptCompletionKey(
        string key,
        bool ctrl,
        bool alt,
        bool shift,
        bool expected)
    {
        var args = new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs
        {
            Key = key,
            CtrlKey = ctrl,
            AltKey = alt,
            ShiftKey = shift,
        };

        Assert.Equal(expected, DashboardFilterCommandKeys.IsAcceptCompletion(args));
    }

    [Fact]
    public void SelectFieldFilterCommand_applies_single_value_by_filter_id()
    {
        var uiState = new DashboardFilterUiState();
        var context = CreateContext(
            uiState,
            ["app_name"],
            options: new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["app_name"] = ["AutoCAD", "Revit"],
            });
        context.CanonicalPath = FilterCommandPaths.FilterPath("app_name");
        context.ArgTail = "AutoCAD";

        var command = new SelectFieldFilterCommand("app_name");
        var outcome = command.ExecuteAsync(context).AsTask().GetAwaiter().GetResult();

        Assert.True(outcome.Success, outcome.Error);
        Assert.Equal(["AutoCAD"], uiState.SelectedFields["app_name"]);
    }

    [Fact]
    public void Catalog_loads_date_filter_from_toolbar()
    {
        var uiState = new DashboardFilterUiState();
        var context = CreateContext(uiState, ["usage_date"]);
        var catalog = DashboardCommandCatalogBuilder.Build(context, []);

        Assert.True(catalog.TryGet(FilterCommandPaths.FilterPath("usage_date"), out var route));
        Assert.Equal(SelectDateFilterCommand.Id, route.CommandId);
        Assert.Equal(SlashArgTailKind.Picker, route.ArgTailKind);
        Assert.Contains("today", route.ResolvedPickerChoices.Select(choice => choice.Value));
    }

    [Fact]
    public void Catalog_exposes_field_filter_by_id()
    {
        var uiState = new DashboardFilterUiState();
        var context = CreateContext(uiState, ["app_name"]);
        var catalog = DashboardCommandCatalogBuilder.Build(context, []);

        Assert.True(catalog.TryGet(FilterCommandPaths.FilterPath("app_name"), out var route));
        Assert.Equal("dash.select.filter.app_name", route.CommandId);
        Assert.Equal("picker:dash.field.app_name", route.ArgTail);
    }

    [Fact]
    public void Completion_on_select_filter_with_trailing_space_lists_toolbar_filters()
    {
        var uiState = new DashboardFilterUiState();
        var context = CreateContext(uiState, ["usage_date", "user_name", "app_name"]);
        var catalog = DashboardCommandCatalogBuilder.Build(context, []);
        var result = DashboardFilterSlashCompletion.GetResult(catalog, context, "select filter ", null);

        Assert.Equal(SlashInputMode.Path, result.Guidance.Mode);
        Assert.Equal(3, result.Items.Count);
        Assert.Contains(result.Items, item => item.StepSegment == "usage_date");
    }

    [Fact]
    public void Completion_on_select_lists_branches()
    {
        var uiState = new DashboardFilterUiState();
        var context = CreateContext(uiState, ["usage_date", "user_name", "app_name"]);
        var catalog = DashboardCommandCatalogBuilder.Build(context, []);
        var result = DashboardFilterSlashCompletion.GetResult(catalog, context, "select", null);

        Assert.Equal(SlashInputMode.Path, result.Guidance.Mode);
        Assert.Single(result.Items);
        Assert.Equal("filter", result.Items[0].StepSegment);
    }

    [Fact]
    public void FormatSuggestion_shows_next_segment_only()
    {
        var item = new SlashCompletionItem(
            "select filter usage_date ",
            "select filter usage_date",
            "Дата отчёта",
            "Filter",
            "usage_date");

        Assert.Equal("usage_date", DashboardFilterCommandDisplay.FormatSuggestion(item));
        Assert.Equal("today", DashboardFilterCommandDisplay.FormatSuggestion(
            new SlashCompletionItem("/select filter usage_date today", "select filter usage_date", "Today", "Filter", "today", SlashCompletionItemKind.Picker, "today")));
    }

    [Fact]
    public void Completion_on_select_filter_lists_toolbar_filters()
    {
        var uiState = new DashboardFilterUiState();
        var context = CreateContext(
            uiState,
            ["usage_date", "user_name", "app_name"],
            labels: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["usage_date"] = "Дата отчёта",
                ["app_name"] = "Продукты",
                ["user_name"] = "Пользователь",
            });
        var catalog = DashboardCommandCatalogBuilder.Build(context, []);
        var result = DashboardFilterSlashCompletion.GetResult(catalog, context, "select filter", null);

        Assert.Equal(3, result.Items.Count);
        Assert.Contains(result.Items, item => item.StepSegment == "usage_date");
        Assert.All(result.Items, item => Assert.Equal(item.StepSegment, DashboardFilterCommandDisplay.FormatSuggestion(item)));
        Assert.Equal("select filter", result.Guidance.Breadcrumb);
        Assert.Equal("<id> · <value>", result.Guidance.Hint);
    }

    [Theory]
    [InlineData("/select", "select")]
    [InlineData("> select", "select")]
    [InlineData(" /select ", "select ")]
    [InlineData("/select filter usage_date", "select filter usage_date")]
    [InlineData("select filter program", "select filter program")]
    [InlineData("select select filter location", "select filter location")]
    public void SanitizeLine_strips_prompt_and_duplicate_select(string input, string expected)
    {
        Assert.Equal(expected, DashboardFilterSlashCompletion.SanitizeLine(input));
    }

    [Fact]
    public void Completion_tolerates_legacy_slash_prefix()
    {
        var uiState = new DashboardFilterUiState();
        var context = CreateContext(uiState, ["usage_date", "user_name", "app_name"]);
        var catalog = DashboardCommandCatalogBuilder.Build(context, []);
        var result = DashboardFilterSlashCompletion.GetResult(catalog, context, "/select", null);

        Assert.Single(result.Items);
        Assert.Equal("filter", result.Items[0].StepSegment);
    }

    [Fact]
    public void Completion_on_select_filter_date_space_enters_picker_mode()
    {
        var uiState = new DashboardFilterUiState();
        var context = CreateContext(uiState, ["usage_date"]);
        var catalog = DashboardCommandCatalogBuilder.Build(context, []);
        var result = DashboardFilterSlashCompletion.GetResult(
            catalog,
            context,
            $"{FilterCommandPaths.FilterPath("usage_date")} ",
            null);

        Assert.Equal(SlashInputMode.Picker, result.Guidance.Mode);
        Assert.Contains(result.Items, item => item.PickValue == "today");
    }

    [Fact]
    public void ToCommandLine_builds_executable_command()
    {
        Assert.Equal(
            "select filter usage_date today",
            DashboardFilterSlashCompletion.ToCommandLine("filter usage_date today"));
        Assert.Equal("select", DashboardFilterSlashCompletion.ToCommandLine(""));
        Assert.Equal(
            "select filter usage_date today",
            DashboardFilterSlashCompletion.ToCommandLine("> select filter usage_date today"));
    }

    [Fact]
    public void GetSuggestions_lists_date_presets_after_path()
    {
        var uiState = new DashboardFilterUiState();
        var context = CreateContext(uiState, ["usage_date"]);
        var catalog = DashboardCommandCatalogBuilder.Build(context, []);
        var items = SlashStepCompletion.GetSuggestions(
            catalog,
            $"{FilterCommandPaths.FilterPath("usage_date")} ");

        Assert.Contains(items, item => item.PickValue == "today");
        Assert.Contains(items, item => item.PickValue == "last-week");
    }

    [Fact]
    public void GetResult_enters_picker_mode_for_field_filter()
    {
        var uiState = new DashboardFilterUiState();
        var session = new StubDashboardSession();
        var context = CreateContext(
            uiState,
            ["app_name"],
            options: new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["app_name"] = ["AutoCAD", "Revit"],
            });
        var catalog = DashboardCommandCatalogBuilder.Build(context, []);
        var picker = new DashboardFilterPickerSource(session, ["app_name"]);
        var result = SlashCompletion.GetResult(
            catalog,
            $"{FilterCommandPaths.FilterPath("app_name")} ",
            picker);

        Assert.Equal(SlashInputMode.Picker, result.Guidance.Mode);
        Assert.Contains(result.Items, item => item.PickValue == "AutoCAD");
    }

    [Fact]
    public void Executor_rejects_incomplete_field_command()
    {
        var uiState = new DashboardFilterUiState();
        var context = CreateContext(uiState, ["app_name"]);
        var catalog = DashboardCommandCatalogBuilder.Build(context, []);
        var executor = new DashboardCommandExecutor(new DashSpecCommandPluginRegistry());

        var outcome = executor.TryExecuteSlashLine(FilterCommandPaths.FilterPath("app_name"), context, catalog);

        Assert.False(outcome.Success);
        Assert.Contains("аргумент", outcome.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateRunnable_blocks_incomplete_command()
    {
        var uiState = new DashboardFilterUiState();
        var context = CreateContext(uiState, ["app_name"]);
        var catalog = DashboardCommandCatalogBuilder.Build(context, []);
        var executor = new DashboardCommandExecutor(new DashSpecCommandPluginRegistry());

        Assert.False(executor.TryValidateRunnable(FilterCommandPaths.FilterPath("app_name"), catalog, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void Executor_runs_slash_line_through_registry()
    {
        var uiState = new DashboardFilterUiState();
        var context = CreateContext(uiState, ["usage_date"]);
        var catalog = DashboardCommandCatalogBuilder.Build(context, []);
        var executor = new DashboardCommandExecutor(new DashSpecCommandPluginRegistry());

        var outcome = executor.TryExecuteSlashLine(
            $"{FilterCommandPaths.FilterPath("usage_date")} today",
            context,
            catalog);

        Assert.True(outcome.Success, outcome.Error);
        Assert.Equal(new DateOnly(2026, 6, 24), uiState.DateFrom["usage_date"]);
        Assert.Equal(new DateOnly(2026, 6, 24), uiState.DateTo["usage_date"]);
    }

    [Fact]
    public void Executor_field_command_syncs_to_session_filter_state()
    {
        var uiState = new DashboardFilterUiState();
        var session = new StubDashboardSession();
        var context = CreateContext(
            uiState,
            ["app_name"],
            options: new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["app_name"] = ["AutoCAD", "Revit"],
            });
        var catalog = DashboardCommandCatalogBuilder.Build(context, []);
        var executor = new DashboardCommandExecutor(new DashSpecCommandPluginRegistry());

        var outcome = executor.TryExecuteSlashLine(
            $"{FilterCommandPaths.FilterPath("app_name")} Revit",
            context,
            catalog);

        Assert.True(outcome.Success, outcome.Error);
        uiState.SyncToSession(session, ["app_name"]);
        var field = session.Filters.GetField("app_name");
        Assert.NotNull(field);
        Assert.Equal(["Revit"], field.Value.Values);
    }

    static DashboardFilterContext CreateContext(
        DashboardFilterUiState uiState,
        IReadOnlyList<string> toolbarFilters,
        IReadOnlyDictionary<string, string>? aliases = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? options = null,
        IReadOnlyDictionary<string, string>? labels = null)
    {
        var filterIndex = new Dictionary<string, FilterDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["usage_date"] = new(
                FilterKind.Date,
                "usage_date",
                "-7d..today",
                "usage_date",
                Label: labels?.GetValueOrDefault("usage_date")),
            ["app_name"] = new(
                FilterKind.Field,
                "app_name",
                null,
                "app_name",
                Label: labels?.GetValueOrDefault("app_name"),
                Widget: "chips"),
            ["user_name"] = new(
                FilterKind.Field,
                "user_name",
                null,
                "user_name",
                Label: labels?.GetValueOrDefault("user_name"),
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

    sealed class StubDashboardSession : Services.Abstractions.IDashboardSession
    {
        public StubDashboardSession(IReadOnlyDictionary<string, string>? aliases = null)
        {
            Document = new DashboardDocument(
                Id: "demo",
                Title: "Demo",
                ConnectorId: "stub",
                SqlDialect: SqlDialect.TSql,
                DiagramLibraryPath: null,
                PalettePath: null,
                ColorPalette: null,
                Layout: new LayoutDefinition(),
                FiltersChrome: FiltersChromeDefinition.Default,
                Filters: [],
                DashboardFilters: [],
                Tabs: [],
                Cards: [],
                CommandAliases: aliases);
            Filters = new FilterState();
            FilterIndex = new Dictionary<string, FilterDefinition>(StringComparer.OrdinalIgnoreCase)
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
        }

        public Core.Parsing.SpecLibrary? SpecLibrary => null;
        public DashboardDocument Document { get; }
        public FilterState Filters { get; }
        public string ActiveConnectorId => "stub";
        public string? LoadedSpecSource => null;
        public string? ActiveCatalogEntryId => null;
        public string? CurrentSpecReference => null;
        public IReadOnlyDictionary<string, FilterDefinition> FilterIndex { get; }

        public Task LoadAsync(string? specRelativePath = null, CancellationToken cancellationToken = default, Services.Loading.SpecLoadOptions? options = null) =>
            Task.CompletedTask;

        public Task LoadCatalogEntryAsync(string entryId, CancellationToken cancellationToken = default, Services.Loading.SpecLoadOptions? options = null) =>
            Task.CompletedTask;

        public Task LoadFromUploadAsync(Stream stream, string fileName, CancellationToken cancellationToken = default, Services.Loading.SpecLoadOptions? options = null) =>
            Task.CompletedTask;

        public Task RefreshFieldOptionsAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public IReadOnlyList<string> GetFieldOptions(string filterName) =>
            filterName.Equals("app_name", StringComparison.OrdinalIgnoreCase)
                ? ["AutoCAD", "Revit"]
                : [];

        public void ApplyDateFilter(string name, DateOnly from, DateOnly to) =>
            Filters.SetDate(name, from, to);

        public void ApplyFieldFilter(string name, IEnumerable<string> values) =>
            Filters.SetField(name, values.ToList());

        public void ApplyTopFilter(string name, int limit) =>
            Filters.SetTop(name, limit);

        public Task<Services.Models.CardRenderResult> RenderCardAsync(CardDefinition card, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
