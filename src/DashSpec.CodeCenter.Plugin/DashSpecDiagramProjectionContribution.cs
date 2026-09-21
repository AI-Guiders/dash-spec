using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AIGuiders.Platform.Modeling.Core.Identity;
using AIGuiders.Platform.Modeling.LanguageIntelligence.Relations;
using AIGuiders.Surface.Wpf.Abstractions;
using AIGuiders.Surface.Wpf.CodeCenter;

namespace DashSpec.CodeCenter.Plugin;

public sealed class DashSpecDiagramProjectionContribution : ICodeCenterProjectionContribution
{
    readonly ScrollViewer _scrollViewer;
    readonly WrapPanel _wrapPanel;
    IDocumentSession? _session;

    public DashSpecDiagramProjectionContribution()
    {
        _wrapPanel = new WrapPanel();
        _scrollViewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = _wrapPanel,
        };
    }

    public ProjectionKind Kind => ProjectionKind.Diagram;

    public string PluginId => "dashspec.diagram";

    public FrameworkElement View => _scrollViewer;

    public string DiagramId { get; private set; } = string.Empty;

    public IReadOnlyList<DocumentGraphNodeView> Boxes { get; private set; } = Array.Empty<DocumentGraphNodeView>();

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
        _wrapPanel.Children.Clear();
        if (_session is null)
        {
            DiagramId = string.Empty;
            Boxes = Array.Empty<DocumentGraphNodeView>();
            return;
        }

        Boxes = DashSpecGraphProjection.ReadDiagramBoxes(_session);
        DiagramId = $"dashspec-diagram:{_session.DocumentId}:r{_session.Revision}:n{Boxes.Count}";

        foreach (var node in Boxes)
        {
            _wrapPanel.Children.Add(CreateBox(node));
        }
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
    }

    Border CreateBox(DocumentGraphNodeView node)
    {
        var border = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x56, 0x9C, 0xD6)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10, 6, 10, 6),
            Margin = new Thickness(4),
            Background = new SolidColorBrush(Color.FromArgb(0x1A, 0xC5, 0x86, 0xC0)),
            Tag = node.Id,
            Child = new TextBlock
            {
                Text = DocumentGraphProjection.FormatNodeLabel(node),
                FontWeight = FontWeights.SemiBold,
            },
        };
        border.MouseLeftButtonUp += OnBoxClick;
        return border;
    }

    void OnBoxClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement element || element.Tag is not Identity<SyntaxNode, NumericId> nodeId)
        {
            return;
        }

        if (e.ClickCount >= 2)
        {
            NavigationRequested?.Invoke(this, GraphNodeRefMapping.ToAnchor(nodeId));
            e.Handled = true;
            return;
        }

        SelectionChanged?.Invoke(this, GraphNodeRefMapping.ToAnchor(nodeId));
    }
}
