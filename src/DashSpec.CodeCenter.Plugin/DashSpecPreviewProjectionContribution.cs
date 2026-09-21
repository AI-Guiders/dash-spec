using System.Windows;
using System.Windows.Controls;
using AIGuiders.Surface.Wpf.Abstractions;
using AIGuiders.Surface.Wpf.CodeCenter;

namespace DashSpec.CodeCenter.Plugin;

public sealed class DashSpecPreviewProjectionContribution : ICodeCenterProjectionContribution
{
    readonly ScrollViewer _scrollViewer;
    readonly TextBlock _textBlock;
    IDocumentSession? _session;

    public DashSpecPreviewProjectionContribution()
    {
        _textBlock = new TextBlock
        {
            FontFamily = new System.Windows.Media.FontFamily("Cascadia Code, Consolas"),
            FontSize = 14,
            TextWrapping = TextWrapping.NoWrap,
            Margin = new Thickness(4),
        };
        _scrollViewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _textBlock,
        };
    }

    public ProjectionKind Kind => ProjectionKind.Preview;

    public string PluginId => "dashspec.preview";

    public FrameworkElement View => _scrollViewer;

    public string PreviewText { get; private set; } = string.Empty;

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
            PreviewText = string.Empty;
            _textBlock.Text = string.Empty;
            return;
        }

        PreviewText = DashSpecGraphProjection.BuildPreviewText(_session);
        _textBlock.Text = PreviewText;
    }

    public void ShowLocation(LanguageLocus locus)
    {
    }

    public bool TryMapInput(ProjectionHit hit, out SessionAnchor anchor)
    {
        anchor = default;
        return false;
    }

    public void Dispose()
    {
    }
}
