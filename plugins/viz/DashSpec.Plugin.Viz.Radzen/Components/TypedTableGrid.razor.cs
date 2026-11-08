using System.Collections.Concurrent;
using System.Reflection;
using DashSpec.Abstractions.Data;
using DashSpec.Execution.Runtime;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;

namespace DashSpec.Plugin.Viz.RadzenUI.Components;

/// <summary>
/// The table grid — the same chrome it always had (ADR-0115 header menu with
/// copy-per-column, ADR-0107 row click, LabelFormat cells with the report defaults
/// carried on the render result, ADR-0079 §3). ADR-0124 changed only the payload:
/// rows arrive as CLR objects (compiled row types), so Radzen's native sort/filter/
/// grouping run on real properties — no hand-built runs, no LoadData staging.
/// </summary>
public sealed partial class TypedTableGrid<TItem> where TItem : notnull
{
    private const int GridPageSize = 15;

    [Parameter] public IReadOnlyList<TableField> Fields { get; set; } = [];

    [Parameter] public IReadOnlyList<TItem> Items { get; set; } = [];

    [Parameter] public bool ColumnFilters { get; set; } = true;

    [Parameter] public int FrozenColumns { get; set; } = 0;

    /// <summary>Report defaults carried on the render result — LabelFormat's ambient
    /// is not trustworthy outside the payload-building flow.</summary>
    [Parameter] public ReportFormatDefaults? FormatDefaults { get; set; }

    /// <summary>ADR-0107: the card declares a `row`-sourced click handler → rows are clickable.</summary>
    [Parameter] public bool RowClickEnabled { get; set; }

    /// <summary>The first column's formatted value rides the same contract as the
    /// builtin table (value-based, a re-render can't make it stale).</summary>
    [Parameter] public EventCallback<string> OnRowClick { get; set; }

    private RadzenDataGrid<TItem>? _grid;

    private RadzenContextMenu? _columnMenu;

    private int _menuColumn;

    private int? _copiedColumn;

    private static readonly ConcurrentDictionary<Type, IReadOnlyDictionary<string, PropertyInfo>> PropertyCache =
        new();

    private static IReadOnlyDictionary<string, PropertyInfo> PropertiesOf(Type type) =>
        PropertyCache.GetOrAdd(type, t => t.GetProperties().ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase));

    private object? PropertyOf(TItem row, int fieldIndex) =>
        PropertiesOf(typeof(TItem)).TryGetValue(Fields[fieldIndex].Name, out var property)
            ? property.GetValue(row)
            : null;

    private string FormatCell(object? value, int fieldIndex) =>
        LabelFormat.FormatObject(value, Fields[fieldIndex].Format, FormatDefaults);

    private TextAlign ColumnTextAlign(int index) =>
        Fields[index].Alignment switch
        {
            "right" => TextAlign.Right,
            "center" => TextAlign.Center,
            _ => TextAlign.Left,
        };

    private async Task OnRowClicked(DataGridRowMouseEventArgs<TItem> args)
    {
        if (!RowClickEnabled || args.Data is not { } row)
        {
            return;
        }

        await OnRowClick.InvokeAsync(FormatCell(PropertyOf(row, 0), 0));
    }

    private async Task OpenColumnMenu(Microsoft.AspNetCore.Components.Web.MouseEventArgs args, int columnIndex)
    {
        _menuColumn = columnIndex;
        if (_columnMenu is null)
        {
            return;
        }

        var items = new List<ContextMenuItem>
        {
            new() { Text = Loc["Sort ascending"], Value = "sort-asc" },
            new() { Text = Loc["Sort descending"], Value = "sort-desc" },
            new() { Text = Loc["Copy column"], Value = "copy" },
        };

        await _columnMenu.Open(args, new ContextMenuOptions { Items = items, Click = OnColumnMenuClick });
    }

    private void OnColumnMenuClick(MenuItemEventArgs args)
    {
        var column = Fields[_menuColumn].Name;
        switch (args.Value as string)
        {
            case "sort-asc":
            case "sort-desc":
                _ = InvokeAsync(async () =>
                {
                    if (_grid is not null)
                    {
                        _grid.OrderBy($"{column} {(string.Equals(args.Value as string, "sort-desc") ? "desc" : "asc")}");
                        await _grid.Reload();
                    }
                });
                break;
            case "copy":
                _ = InvokeAsync(() => CopyColumnAsync(_menuColumn));
                break;
        }

        _columnMenu?.Close();
    }

    /// <summary>Copy one column's visible values to the clipboard (newline-joined) —
    /// the same file as the builtin table's copy (ADR-0115 §4). The source is the
    /// grid's current view (filtered + sorted), not the current page.</summary>
    private async Task CopyColumnAsync(int columnIndex)
    {
        try
        {
            var rows = _grid?.View as IEnumerable<TItem> ?? Items;
            var text = string.Join(
                Environment.NewLine,
                rows.Select(row => FormatCell(PropertyOf(row, columnIndex), columnIndex)));
            var copied = await Js.InvokeAsync<bool>("dashSpecRadzenTableCopy", text);
            if (!copied)
            {
                return;
            }

            _copiedColumn = columnIndex;
            _ = ResetCopiedAsync();
        }
        catch (JSDisconnectedException)
        {
        }
        catch (JSException)
        {
        }
    }

    private async Task ResetCopiedAsync()
    {
        await Task.Delay(900);
        _copiedColumn = null;
        await InvokeAsync(StateHasChanged);
    }
}
