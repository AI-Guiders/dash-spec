using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AIGuiders.Platform.Modeling.Core.Identity;
using AIGuiders.Platform.Modeling.LanguageIntelligence.Relations;
using AIGuiders.Surface.Wpf.Abstractions;
using AIGuiders.Surface.Wpf.CodeCenter;
using DashSpec.Modeling.CodeCenter;

namespace DashSpec.CodeCenter.Plugin;

public sealed class DashSpecFormProjectionContribution : ICodeCenterProjectionContribution
{
    readonly ScrollViewer _scrollViewer;
    readonly ListView _listView;
    IDocumentSession? _session;

    public DashSpecFormProjectionContribution()
    {
        var grid = new GridView();
        grid.Columns.Add(new GridViewColumn { Header = "Kind", DisplayMemberBinding = new System.Windows.Data.Binding(nameof(FormFieldRow.Kind)), Width = 120 });
        grid.Columns.Add(new GridViewColumn { Header = "Name", DisplayMemberBinding = new System.Windows.Data.Binding(nameof(FormFieldRow.Name)), Width = 200 });
        grid.Columns.Add(new GridViewColumn { Header = "Range", DisplayMemberBinding = new System.Windows.Data.Binding(nameof(FormFieldRow.RangeLabel)), Width = 120 });

        _listView = new ListView { View = grid };
        _listView.MouseDoubleClick += OnMouseDoubleClick;
        _scrollViewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = _listView,
        };
    }

    public ProjectionKind Kind => ProjectionKind.Form;

    public string PluginId => "dashspec.form";

    public FrameworkElement View => _scrollViewer;

    public IReadOnlyList<DocumentGraphNodeView> Fields { get; private set; } = Array.Empty<DocumentGraphNodeView>();

    public event EventHandler<SessionAnchor>? SelectionChanged;

    public event EventHandler<SessionAnchor>? NavigationRequested;

    public void Bind(IDocumentSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
        Refresh();
    }

    public void Refresh()
    {
        if (_session is null)
        {
            Fields = Array.Empty<DocumentGraphNodeView>();
            _listView.ItemsSource = null;
            return;
        }

        Fields = DashSpecGraphProjection.ReadFormFields(_session);
        _listView.ItemsSource = Fields
            .Select(node => new FormFieldRow(node.Id, OutlineKeyword(node.Name), node.Name, node.Range))
            .ToArray();
    }

    public void ShowLocation(LanguageLocus locus)
    {
    }

    public bool TryMapInput(ProjectionHit hit, out SessionAnchor anchor)
    {
        anchor = default;
        if (hit.Target != ProjectionHitKind.TreeNode || hit.NodeCounter is not { } counter)
        {
            return false;
        }

        anchor = SessionAnchor.FromGraphNode(new GraphNodeRef(counter));
        return true;
    }

    public void Dispose()
    {
        _listView.MouseDoubleClick -= OnMouseDoubleClick;
    }

    void OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_listView.SelectedItem is not FormFieldRow row)
        {
            return;
        }

        NavigationRequested?.Invoke(this, GraphNodeRefMapping.ToAnchor(row.Id));
        e.Handled = true;
    }

    static string OutlineKeyword(string name)
    {
        var index = name.IndexOf(' ');
        return index < 0 ? name : name[..index];
    }

    sealed record FormFieldRow(Identity<SyntaxNode, NumericId> Id, string Kind, string Name, LineRange Range)
    {
        public string RangeLabel => $"{Range.Start}-{Range.End}";
    }
}
