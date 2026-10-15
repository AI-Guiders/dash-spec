using System.Collections.Concurrent;
using DashSpec.Core.Validation;

namespace DashSpec.LanguageServer;

internal sealed class DashSpecServerState
{
    private readonly ConcurrentDictionary<string, string> _documents =
        new(StringComparer.OrdinalIgnoreCase);

    private DashSpecWorkspaceIndex _index = new();

    public DashSpecWorkspaceIndex Index => _index;

    public void SetWorkspaceRoots(IEnumerable<string> roots)
    {
        var merged = new DashSpecWorkspaceIndex();
        foreach (var root in roots.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var scanned = DashSpecWorkspaceIndex.Scan(root);
            foreach (var (id, path) in scanned.Diagrams)
            {
                merged.RegisterDiagram(id, path);
            }

            foreach (var (id, path) in scanned.Presentations)
            {
                merged.RegisterPresentation(id, path);
            }
        }

        _index = merged;
    }

    public void RescanWorkspaceRoots(IEnumerable<string> roots) => SetWorkspaceRoots(roots);

    public void OpenDocument(string path, string text)
    {
        _documents[path] = text;
        RegisterFromOpenDocument(path, text);
    }

    public void ChangeDocument(string path, string text) => _documents[path] = text;

    public void CloseDocument(string path) => _documents.Remove(path, out _);

    public string? GetDocumentText(string path) =>
        _documents.TryGetValue(path, out var text) ? text : null;

    private void RegisterFromOpenDocument(string path, string text)
    {
        if (!Path.GetExtension(path).Equals(".ds", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var diagramId = DashSpecWorkspaceIndex.TryReadDiagramId(text);
        if (!string.IsNullOrWhiteSpace(diagramId))
        {
            _index.RegisterDiagram(diagramId, path);
            return;
        }

        var presentationId = DashSpecWorkspaceIndex.TryReadPresentationId(text);
        if (!string.IsNullOrWhiteSpace(presentationId))
        {
            _index.RegisterPresentation(presentationId, path);
        }
    }
}
