using AIGuiders.Platform.IntermediateRepresentation.Invocation;
using AIGuiders.Platform.CommandPlane;
using AIGuiders.Platform.CommandPlane.ArgSuggestions;
using DashSpec.Core.Model;
using DashSpec.Core.Runtime;
using DashSpec.Host.Commands;
using DashSpec.Host.Commands.Constructors;
using DashSpec.Host.Services.Presentation;
using Xunit;

namespace DashSpec.Host.Tests;

public class DashboardFilterCommandTests
{
    [Theory]
    [InlineData("today")]
    [InlineData("last-week")]
    [InlineData("2026-07")]
    [InlineData("2026-W26")]
    [InlineData("2026-08-M2")]
    [InlineData("W26")]
    [InlineData("2026-Q1")]
    [InlineData("2026-q2")]
    [InlineData("Q3")]
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
    public void ToInputTail_keeps_command_verbs_in_tail()
    {
        Assert.Equal("", DashboardFilterSlashCompletion.ToInputTail(""));
        Assert.Equal("select filter usage_date", DashboardFilterSlashCompletion.ToInputTail("select filter usage_date"));
        Assert.Equal("select filter", DashboardFilterSlashCompletion.ToInputTail("> select filter"));
    }

    [Fact]
    public void ShowHostSurfaceCommand_sets_pending_route()
    {
        var context = CreateContext(new DashboardFilterUiState(), []);
        context.CanonicalPath = ShowCommandPaths.SurfacePath("controlcenter");
        context.ArgTail = "";

        var command = new ShowHostSurfaceCommand();
        var outcome = command.ExecuteAsync(context).AsTask().GetAwaiter().GetResult();

        Assert.True(outcome.Success, outcome.Error);
        Assert.Equal("/admin/access", context.PendingHostRoute);
    }

    [Fact]
    public void Root_completion_includes_show_verb()
    {
        var service = CreateCommandService(new DashboardFilterUiState());
        var context = CreateContext(new DashboardFilterUiState(), []);
        var result = service.GetCompletionResult("", context);
        Assert.Contains(result.Items, item =>
            item.StepSegment == ShowCommandPaths.RootVerb
            || DashboardFilterSlashCompletion.LineFromInsert(item.InsertText)
                .StartsWith("show ", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Show_host_completion_uses_console_phrase_from_catalog()
    {
        var hostContext = HostCommandContextFactory.CreateHostOnly(new DashboardFilterUiState(), TestCulture);
        var catalog = DashboardCommandCatalogBuilder.Build(hostContext, []);
        var result = DashboardFilterSlashCompletion.GetResult(catalog, hostContext, "show", null, null);

        Assert.Contains(result.Items, item => item.StepSegment == ShowCommandPaths.HostBranch);
        Assert.Equal("show host dashboard", ShowCommandPaths.SurfacePath("dashboard"));
        Assert.Equal("show host controlcenter", ShowCommandPaths.SurfacePath("controlcenter"));
        Assert.True(catalog.TryGet(ShowCommandPaths.SurfacePath("dashboard"), out _));
    }

    [Fact]
    public void Catalog_flavor_is_console_with_matching_ccl_grammar()
    {
        Assert.True(DashboardCatalogFlavor.IsConsole);
        Assert.Equal(DashboardCatalogFlavor.ConsoleGrammar, DashboardCatalogFlavor.CclCommandGrammar);
    }

    [Fact]
    public void Catalog_bindings_resolve_suggest_dismiss_gesture()
    {
        Assert.Equal("Ctrl+.", DashboardCatalogBindings.SuggestDismissGesture);
        Assert.Equal("Ctrl+K", DashboardCatalogBindings.ChordRootGesture);
        Assert.Contains(DashboardCatalogBindings.HostBindings(), binding => binding.MethodName == "OnSuggestDismiss");
    }

    [Fact]
    public void Catalog_host_scope_excludes_dashboard_paths()
    {
        var hostContext = HostCommandContextFactory.CreateHostOnly(new DashboardFilterUiState(), TestCulture);
        var catalog = DashboardCommandCatalogBuilder.Build(hostContext, []);

        Assert.True(catalog.TryGet(ShowCommandPaths.SurfacePath("dashboard"), out _));
        Assert.True(catalog.TryGet(ShowCommandPaths.SurfacePath("controlcenter"), out _));
        Assert.False(catalog.TryGet(FilterCommandPaths.FilterPath("usage_date"), out _));
        Assert.False(catalog.TryGet($"{FilterCommandPaths.RootVerb} filter", out _));
    }

    [Fact]
    public void Root_completion_requires_dashboard_scope_for_filter_and_select()
    {
        var service = CreateCommandService(new DashboardFilterUiState());
        var hostContext = HostCommandContextFactory.CreateHostOnly(new DashboardFilterUiState(), TestCulture);
        var dashboardContext = CreateContext(
            new DashboardFilterUiState(),
            ["usage_date", "app_name"]);

        var hostResult = service.GetCompletionResult("", hostContext);
        var dashboardResult = service.GetCompletionResult("", dashboardContext);

        Assert.DoesNotContain(
            hostResult.Items,
            item => string.Equals(item.StepSegment, FilterCommandPaths.RootVerb, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(item.StepSegment, "select", StringComparison.OrdinalIgnoreCase));

        Assert.Contains(
            dashboardResult.Items,
            item => string.Equals(item.StepSegment, "select", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(item.StepSegment, FilterCommandPaths.RootVerb, StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
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
        Assert.Equal(CommandArgTailKind.Picker, route.ArgTailKind);
        Assert.Contains(
            route.ResolvedConstructors,
            binding => binding.ConstructorId == DateConstructorCatalog.DateTodayId);
        Assert.Empty(route.ResolvedPickerChoices);
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
        var result = DashboardFilterSlashCompletion.GetResult(catalog, context, "select filter ", null, null);

        Assert.Equal(InvocationLinePhase.Path, result.Guidance.Phase);
        Assert.Equal(3, result.Items.Count);
        Assert.Contains(result.Items, item => item.StepSegment == "usage_date");
    }

    [Fact]
    public void Completion_on_select_lists_branches()
    {
        var uiState = new DashboardFilterUiState();
        var context = CreateContext(uiState, ["usage_date", "user_name", "app_name"]);
        var catalog = DashboardCommandCatalogBuilder.Build(context, []);
        var result = DashboardFilterSlashCompletion.GetResult(catalog, context, "select", null, null);

        Assert.Equal(InvocationLinePhase.Path, result.Guidance.Phase);
        Assert.Single(result.Items);
        Assert.Equal("filter", result.Items[0].StepSegment);
    }

    [Fact]
    public void FormatSuggestion_shows_human_label_with_id_badge()
    {
        var uiState = new DashboardFilterUiState();
        var context = CreateContext(
            uiState,
            ["usage_date"],
            labels: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["usage_date"] = "Дата отчёта",
            });
        var item = new ArgCompletionItem(
            "select filter usage_date ",
            "select filter usage_date",
            "Дата отчёта",
            "Filter",
            "usage_date");

        var parts = DashboardFilterCommandDisplay.FormatSuggestionParts(item, context);
        Assert.Equal("Дата отчёта", parts.Primary);
        Assert.Equal("usage_date", parts.Secondary);
        Assert.Equal("today", DashboardFilterCommandDisplay.FormatSuggestion(
            new ArgCompletionItem("/select filter usage_date today", "select filter usage_date", "Today", "Filter", "today", ArgCompletionItemKind.Picker, "today"),
            context));
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
        var result = DashboardFilterSlashCompletion.GetResult(catalog, context, "select filter", null, null);

        Assert.Equal(3, result.Items.Count);
        Assert.Contains(result.Items, item => item.StepSegment == "usage_date");
        Assert.All(result.Items, item =>
        {
            var parts = DashboardFilterCommandDisplay.FormatSuggestionParts(item, context);
            Assert.False(string.IsNullOrWhiteSpace(parts.Primary));
        });
        Assert.Equal("select filter", result.Guidance.Breadcrumb);
        Assert.Equal("название фильтра · значение", result.Guidance.Hint);
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
        var result = DashboardFilterSlashCompletion.GetResult(catalog, context, "/select", null, null);

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
            null,
            null);

        Assert.Equal(ArgMechanic.Picker, result.Guidance.ArgMechanic);
        Assert.Equal(InvocationLinePhase.Arg, result.Guidance.Phase);
        Assert.Contains(
            result.Items,
            item => item.Kind == ArgCompletionItemKind.ConstructorEntry
                    && item.PickValue == DateConstructorCatalog.DateTodayId);
    }

    [Fact]
    public void ToCommandLine_builds_executable_command()
    {
        Assert.Equal(
            "select filter usage_date today",
            DashboardFilterSlashCompletion.ToCommandLine("filter usage_date today"));
        Assert.Equal("", DashboardFilterSlashCompletion.ToCommandLine(""));
        Assert.Equal(
            "select filter usage_date today",
            DashboardFilterSlashCompletion.ToCommandLine("> select filter usage_date today"));
        Assert.Equal("view card_a heatmap", DashboardFilterSlashCompletion.ToCommandLine("view card_a heatmap"));
    }

    [Fact]
    public void GetSuggestions_lists_date_constructors_after_path()
    {
        var uiState = new DashboardFilterUiState();
        var context = CreateContext(uiState, ["usage_date"]);
        var result = CreateCommandService(uiState).GetCompletionResult(
            $"{FilterCommandPaths.FilterPath("usage_date")} ",
            context);
        var items = result.Items;

        Assert.DoesNotContain(items, item => item.PickValue == "today" && item.Kind == ArgCompletionItemKind.Picker);
        Assert.DoesNotContain(items, item => item.PickValue == "last-week");
        Assert.Contains(
            items,
            item => item.Kind == ArgCompletionItemKind.ConstructorEntry
                    && item.PickValue == DateConstructorCatalog.DateTodayId);
        Assert.Contains(
            items,
            item => item.Kind == ArgCompletionItemKind.ConstructorEntry
                    && item.PickValue == DateConstructorCatalog.DateWeekId);
    }

    [Fact]
    public void DateFilterPresets_resolves_month_week_to_seven_day_blocks_from_first()
    {
        Assert.True(
            DateFilterPresets.TryResolve("2026-08-M2", new DateOnly(2026, 6, 24), out var range, out var error),
            error);
        Assert.Equal(new DateOnly(2026, 8, 8), range.From);
        Assert.Equal(new DateOnly(2026, 8, 14), range.To);

        Assert.True(DateFilterPresets.TryResolve("2026-08-M1", new DateOnly(2026, 6, 24), out var first, out _));
        Assert.Equal(new DateOnly(2026, 8, 1), first.From);
        Assert.Equal(new DateOnly(2026, 8, 7), first.To);
    }

    [Fact]
    public void DateFilterPresets_resolves_iso_week_to_monday_sunday_bounds()
    {
        Assert.True(
            DateFilterPresets.TryResolve("2026-W26", new DateOnly(2026, 6, 24), out var week, out var error),
            error);
        Assert.Equal(DayOfWeek.Monday, week.From.DayOfWeek);
        Assert.Equal(DayOfWeek.Sunday, week.To.DayOfWeek);
        Assert.Equal(week.From.AddDays(6), week.To);

        Assert.True(DateFilterPresets.TryResolve("W26", new DateOnly(2026, 6, 24), out var currentYear, out _));
        Assert.Equal(2026, currentYear.From.Year);
    }

    [Fact]
    public void DateFilterPresets_resolves_quarter_to_calendar_bounds()
    {
        Assert.True(
            DateFilterPresets.TryResolve("2026-Q1", new DateOnly(2026, 6, 24), out var q1, out var error),
            error);
        Assert.Equal(new DateOnly(2026, 1, 1), q1.From);
        Assert.Equal(new DateOnly(2026, 3, 31), q1.To);

        Assert.True(DateFilterPresets.TryResolve("2026-Q4", new DateOnly(2026, 6, 24), out var q4, out _));
        Assert.Equal(new DateOnly(2026, 10, 1), q4.From);
        Assert.Equal(new DateOnly(2026, 12, 31), q4.To);

        Assert.True(DateFilterPresets.TryResolve("Q2", new DateOnly(2026, 6, 24), out var currentYear, out _));
        Assert.Equal(new DateOnly(2026, 4, 1), currentYear.From);
        Assert.Equal(new DateOnly(2026, 6, 30), currentYear.To);
    }

    [Fact]
    public void GetResult_lists_grain_constructor_entries()
    {
        var uiState = new DashboardFilterUiState();
        var context = CreateContext(uiState, ["usage_date"]);
        var service = CreateCommandService(uiState);
        var result = service.GetCompletionResult(
            $"{FilterCommandPaths.FilterPath("usage_date")} ",
            context);

        Assert.Equal(ArgMechanic.Picker, result.Guidance.ArgMechanic);
        Assert.Equal(InvocationLinePhase.Arg, result.Guidance.Phase);
        Assert.Contains(
            result.Items,
            item => item.Kind == ArgCompletionItemKind.ConstructorEntry
                    && item.PickValue == DateConstructorCatalog.DateTodayId);
        Assert.Contains(
            result.Items,
            item => item.Kind == ArgCompletionItemKind.ConstructorEntry
                    && item.PickValue == DateConstructorCatalog.DateWeekId);
        Assert.Contains(
            result.Items,
            item => item.Kind == ArgCompletionItemKind.ConstructorEntry
                    && item.PickValue == DateConstructorCatalog.DateMonthWeekId);
        Assert.Contains(
            result.Items,
            item => item.Kind == ArgCompletionItemKind.ConstructorEntry
                    && item.PickValue == DateConstructorCatalog.DateMonthId);
        Assert.Contains(
            result.Items,
            item => item.Kind == ArgCompletionItemKind.ConstructorEntry
                    && item.PickValue == DateConstructorCatalog.DateQuarterId);
        Assert.Contains(
            result.Items,
            item => item.Kind == ArgCompletionItemKind.ConstructorEntry
                    && item.PickValue == DateConstructorCatalog.DateRangeId);
    }

    [Fact]
    public void Date_range_constructor_emits_wire_and_executes()
    {
        var uiState = new DashboardFilterUiState();
        var context = CreateContext(uiState, ["usage_date"]);
        var host = new DashboardSlashConstructorHost(TestCulture);
        host.SegmentProvider.Today = context.TodayUtc;
        host.Session.Start(
            DateConstructorCatalog.DateRangeId,
            FilterCommandPaths.FilterPath("usage_date"));

        host.Session.TryAdvance("2026");
        host.Session.TryAdvance("08");
        host.Session.TryAdvance("01");
        host.Session.TryAdvance("2026");
        host.Session.TryAdvance("09");
        host.Session.TryAdvance("15");

        Assert.True(host.Session.TryComplete(out var wire));
        Assert.Equal("2026-08-01..2026-09-15", wire);

        var catalog = DashboardCommandCatalogBuilder.Build(context, []);
        var executor = new DashboardCommandExecutor(new DashSpecCommandPluginRegistry());
        var outcome = executor.TryExecuteSlashLine(
            $"{FilterCommandPaths.FilterPath("usage_date")} {wire}",
            context,
            catalog);

        Assert.True(outcome.Success, outcome.Error);
        Assert.True(uiState.DateFrom.ContainsKey("usage_date"));
        Assert.True(uiState.DateTo.ContainsKey("usage_date"));
    }

    [Fact]
    public void Date_month_constructor_emits_wire_and_executes()
    {
        var uiState = new DashboardFilterUiState();
        var context = CreateContext(uiState, ["usage_date"]);
        var host = new DashboardSlashConstructorHost(TestCulture);
        host.SegmentProvider.Today = context.TodayUtc;
        host.Session.Start(
            DateConstructorCatalog.DateMonthId,
            FilterCommandPaths.FilterPath("usage_date"));

        host.Session.TryAdvance("2026");
        host.Session.TryAdvance("07");

        Assert.True(host.Session.TryComplete(out var wire));
        Assert.Equal("2026-07", wire);

        var catalog = DashboardCommandCatalogBuilder.Build(context, []);
        var executor = new DashboardCommandExecutor(new DashSpecCommandPluginRegistry());
        var outcome = executor.TryExecuteSlashLine(
            $"{FilterCommandPaths.FilterPath("usage_date")} {wire}",
            context,
            catalog);

        Assert.True(outcome.Success, outcome.Error);
        Assert.Equal(new DateOnly(2026, 7, 1), uiState.DateFrom["usage_date"]);
        Assert.Equal(new DateOnly(2026, 7, 31), uiState.DateTo["usage_date"]);
    }

    [Fact]
    public void Date_quarter_constructor_emits_wire_and_executes()
    {
        var uiState = new DashboardFilterUiState();
        var context = CreateContext(uiState, ["usage_date"]);
        var host = new DashboardSlashConstructorHost(TestCulture);
        host.SegmentProvider.Today = context.TodayUtc;
        host.Session.Start(
            DateConstructorCatalog.DateQuarterId,
            FilterCommandPaths.FilterPath("usage_date"));

        host.Session.TryAdvance("2026");
        host.Session.TryAdvance("Q2");

        Assert.True(host.Session.TryComplete(out var wire));
        Assert.Equal("2026-Q2", wire);

        var catalog = DashboardCommandCatalogBuilder.Build(context, []);
        var executor = new DashboardCommandExecutor(new DashSpecCommandPluginRegistry());
        var outcome = executor.TryExecuteSlashLine(
            $"{FilterCommandPaths.FilterPath("usage_date")} {wire}",
            context,
            catalog);

        Assert.True(outcome.Success, outcome.Error);
        Assert.Equal(new DateOnly(2026, 4, 1), uiState.DateFrom["usage_date"]);
        Assert.Equal(new DateOnly(2026, 6, 30), uiState.DateTo["usage_date"]);
    }

    [Fact]
    public void Date_week_constructor_emits_wire_and_executes()
    {
        var uiState = new DashboardFilterUiState();
        var context = CreateContext(uiState, ["usage_date"]);
        var host = new DashboardSlashConstructorHost(TestCulture);
        host.SegmentProvider.Today = context.TodayUtc;
        host.Session.Start(
            DateConstructorCatalog.DateWeekId,
            FilterCommandPaths.FilterPath("usage_date"));

        host.Session.TryAdvance("2026");
        host.Session.TryAdvance("26");

        Assert.True(host.Session.TryComplete(out var wire));
        Assert.Equal("2026-W26", wire);

        var catalog = DashboardCommandCatalogBuilder.Build(context, []);
        var executor = new DashboardCommandExecutor(new DashSpecCommandPluginRegistry());
        var outcome = executor.TryExecuteSlashLine(
            $"{FilterCommandPaths.FilterPath("usage_date")} {wire}",
            context,
            catalog);

        Assert.True(outcome.Success, outcome.Error);
        Assert.True(uiState.DateFrom.ContainsKey("usage_date"));
        Assert.True(uiState.DateTo.ContainsKey("usage_date"));
    }

    [Fact]
    public void Date_month_week_constructor_emits_wire_and_executes()
    {
        var uiState = new DashboardFilterUiState();
        var context = CreateContext(uiState, ["usage_date"]);
        var host = new DashboardSlashConstructorHost(TestCulture);
        host.SegmentProvider.Today = context.TodayUtc;
        host.Session.Start(
            DateConstructorCatalog.DateMonthWeekId,
            FilterCommandPaths.FilterPath("usage_date"));

        host.Session.TryAdvance("2026");
        host.Session.TryAdvance("08");
        host.Session.TryAdvance("2");

        Assert.True(host.Session.TryComplete(out var wire));
        Assert.Equal("2026-08-M2", wire);

        var catalog = DashboardCommandCatalogBuilder.Build(context, []);
        var executor = new DashboardCommandExecutor(new DashSpecCommandPluginRegistry());
        var outcome = executor.TryExecuteSlashLine(
            $"{FilterCommandPaths.FilterPath("usage_date")} {wire}",
            context,
            catalog);

        Assert.True(outcome.Success, outcome.Error);
        Assert.Equal(new DateOnly(2026, 8, 8), uiState.DateFrom["usage_date"]);
        Assert.Equal(new DateOnly(2026, 8, 14), uiState.DateTo["usage_date"]);
    }

    [Fact]
    public void Date_today_constructor_entry_commits_immediately()
    {
        var uiState = new DashboardFilterUiState();
        var context = CreateContext(uiState, ["usage_date"]);
        var host = new DashboardSlashConstructorHost(TestCulture);
        var line = FilterCommandPaths.FilterPath("usage_date");
        var item = new ArgCompletionItem(
            "",
            FilterCommandPaths.FilterPath("usage_date"),
            "Сегодня",
            "Filter",
            "Сегодня",
            ArgCompletionItemKind.ConstructorEntry,
            DateConstructorCatalog.DateTodayId);

        Assert.True(DashboardFilterCommandAcceptance.TryAcceptItem(
            item,
            CreateCommandService(uiState),
            context,
            host,
            ref line));
        Assert.Equal($"{FilterCommandPaths.FilterPath("usage_date")} today", line);
        Assert.False(host.Session.IsActive);
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
        var broker = new CommandArgSuggestionRegistry()
            .RegisterPrefix("dash.field.", new DashboardFilterSuggestionProvider(session, ["app_name"]))
            .Build();
        var result = SlashCompletion.GetResult(
            catalog,
            $"{FilterCommandPaths.FilterPath("app_name")} ",
            broker);

        Assert.Equal(ArgMechanic.Picker, result.Guidance.ArgMechanic);
        Assert.Equal(InvocationLinePhase.Arg, result.Guidance.Phase);
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

        Assert.False(executor.TryValidateRunnable(FilterCommandPaths.FilterPath("app_name"), context, catalog, out var error));
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

    [Fact]
    public void Completion_at_empty_root_lists_select_and_view_when_cards_switchable()
    {
        var uiState = new DashboardFilterUiState();
        var context = CreateContext(
            uiState,
            ["usage_date"],
            switchableCards:
            [
                new DashboardCardCommandTarget(
                    "heatmap_card",
                    "Heatmap",
                    [new DashboardCardViewOption("heatmap", "Heatmap"), new DashboardCardViewOption("line", "Line")]),
            ]);
        var catalog = DashboardCommandCatalogBuilder.Build(context, []);
        var result = DashboardFilterSlashCompletion.GetResult(catalog, context, "", null, null);

        Assert.Equal(3, result.Items.Count);
        Assert.Contains(result.Items, item => item.StepSegment == FilterCommandPaths.RootVerb);
        Assert.Contains(result.Items, item => item.StepSegment == ViewCommandPaths.RootVerb);
        Assert.Contains(result.Items, item => item.StepSegment == ShowCommandPaths.RootVerb);
    }

    [Fact]
    public void Normalizer_resolves_filter_label_to_id()
    {
        var uiState = new DashboardFilterUiState();
        var context = CreateContext(
            uiState,
            ["usage_date"],
            labels: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["usage_date"] = "Дата отчёта",
            });

        var normalized = DashboardCommandLineNormalizer.NormalizeExecutableLine(
            "select filter Дата today",
            context);

        Assert.Equal("select filter usage_date today", normalized);
    }

    [Fact]
    public void Executor_runs_view_command_by_card_and_view_labels()
    {
        var uiState = new DashboardFilterUiState();
        var context = CreateContext(
            uiState,
            ["usage_date"],
            switchableCards:
            [
                new DashboardCardCommandTarget(
                    "heatmap_card",
                    "Heatmap card",
                    [new DashboardCardViewOption("heatmap", "Heatmap view"), new DashboardCardViewOption("line", "Line view")]),
            ]);
        var catalog = DashboardCommandCatalogBuilder.Build(context, []);
        var executor = new DashboardCommandExecutor(new DashSpecCommandPluginRegistry());

        var outcome = executor.TryExecuteSlashLine(
            "view Heatmap heatmap",
            context,
            catalog);

        Assert.True(outcome.Success, outcome.Error);
        Assert.Equal("heatmap_card", context.PendingCardId);
        Assert.Equal("heatmap", context.PendingViewId);
    }

    [Fact]
    public void Highlights_focus_single_filter_when_named_in_tail()
    {
        var uiState = new DashboardFilterUiState();
        var context = CreateContext(
            uiState,
            ["usage_date", "app_name"],
            labels: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["usage_date"] = "Дата отчёта",
            });

        var highlights = DashboardCommandHighlightResolver.Resolve("select filter Дата", context);

        Assert.Single(highlights.FilterNames);
        Assert.Contains("usage_date", highlights.FilterNames);
        Assert.Empty(highlights.CardIds);
    }

    [Fact]
    public void Trail_formatter_shows_human_filter_label()
    {
        var uiState = new DashboardFilterUiState();
        var context = CreateContext(
            uiState,
            ["usage_date"],
            labels: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["usage_date"] = "Дата отчёта",
            });

        var trail = DashboardCommandTrailFormatter.Format("select filter usage_date today", context);

        Assert.Collection(
            trail,
            segment => Assert.Equal("select", segment.Label),
            segment => Assert.Equal("filter", segment.Label),
            segment => Assert.Equal("Дата отчёта", segment.Label),
            segment => Assert.Equal("today", segment.Label));
    }

    [Fact]
    public void CommandSession_clears_highlights_when_deactivated()
    {
        var uiState = new DashboardFilterUiState();
        var context = CreateContext(uiState, ["usage_date", "app_name"]);
        var session = CreateCommandSession(uiState);

        session.SetBarActive(true, context);
        session.SetDraftTail("select filter usage_date", context);

        Assert.True(session.IsFilterHighlighted("usage_date"));
        Assert.False(session.IsFilterHighlighted("app_name"));

        session.SetBarActive(false, context);

        Assert.False(session.IsFilterHighlighted("usage_date"));
    }

    [Fact]
    public void CommandSession_keeps_highlights_while_palette_open_after_bar_blur()
    {
        var uiState = new DashboardFilterUiState();
        var context = CreateContext(uiState, ["usage_date"]);
        var session = CreateCommandSession(uiState);

        session.SetBarActive(true, context);
        session.SetDraftTail("select filter usage_date", context);
        session.SetBarActive(false, context);
        session.SetPaletteActive(true, context);
        session.SetDraftTail("select filter usage_date", context);

        Assert.True(session.IsFilterHighlighted("usage_date"));
    }

    [Fact]
    public void Locale_typed_complete_date_enters_ready_mode()
    {
        var uiState = new DashboardFilterUiState();
        var context = CreateContext(uiState, ["usage_date"]);
        var result = CreateCommandService(uiState).GetCompletionResult(
            $"{FilterCommandPaths.FilterPath("usage_date")} 31.08.2026",
            context);

        Assert.Equal(InvocationLinePhase.Ready, result.Guidance.Phase);
        Assert.Equal("2026-08-31", result.Guidance.ReadyWire);
    }

    [Fact]
    public void Locale_typed_month_year_enters_ready_mode()
    {
        var uiState = new DashboardFilterUiState();
        var context = CreateContext(uiState, ["usage_date"]);
        var result = CreateCommandService(uiState).GetCompletionResult(
            $"{FilterCommandPaths.FilterPath("usage_date")} 08.2026",
            context);

        Assert.Equal(InvocationLinePhase.Ready, result.Guidance.Phase);
        Assert.Equal("2026-08", result.Guidance.ReadyWire);
    }

    static DashboardFilterCommandService CreateCommandService(DashboardFilterUiState uiState) =>
        new(
            new StubDashboardSession(),
            uiState,
            new DashboardCommandExecutor(new DashSpecCommandPluginRegistry()),
            DashSpec.Host.Plugins.DashSpecBuiltinContributorRegistrar.RegisterBuiltins(),
            new DashSpecCommandPluginRegistry(),
            new DashboardSlashConstructorHost(TestCulture));

    static readonly DashboardCultureAmbient TestCulture =
        new(System.Globalization.CultureInfo.GetCultureInfo("ru-RU"));

    static DashboardCommandSession CreateCommandSession(DashboardFilterUiState uiState)
    {
        var commandService = CreateCommandService(uiState);
        return new DashboardCommandSession(commandService);
    }

    static DashboardFilterContext CreateContext(
        DashboardFilterUiState uiState,
        IReadOnlyList<string> toolbarFilters,
        IReadOnlyDictionary<string, string>? aliases = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? options = null,
        IReadOnlyDictionary<string, string>? labels = null,
        IReadOnlyList<DashboardCardCommandTarget>? switchableCards = null)
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
            ActiveScope = [DashSpecCommandScope.Dashboard],
            FilterIndex = filterIndex,
            ToolbarFilterNames = toolbarFilters,
            CommandAliases = aliases ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            UiState = uiState,
            GetFieldOptions = name => options?.TryGetValue(name, out var values) == true ? values : [],
            TodayUtc = new DateOnly(2026, 6, 24),
            Culture = TestCulture.Culture,
            SwitchableCards = switchableCards ?? [],
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
