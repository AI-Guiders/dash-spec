using DashSpec.Core.Validation;
using DashSpec.LanguageServer;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace DashSpec.LanguageServer.Handlers;

internal sealed class DashSpecCompletionHandler : ICompletionHandler
{
    private static readonly string[] Keywords =
    [
        "@dashboard",
        "@tab",
        "@diagram",
        "@presentation",
        "@palette",
        "card",
        "diagram",
        "filter",
        "chrome",
        "use",
        "include",
        "!include",
        "end",
        "heatmap",
        "bar",
        "line",
        "table",
        "toolbar",
        "filters",
        "datasource",
        "phase",
        "page",
        "catalog",
    ];

    private readonly DashSpecServerState _state;

    public DashSpecCompletionHandler(DashSpecServerState state) => _state = state;

    public CompletionRegistrationOptions GetRegistrationOptions(
        CompletionCapability capability,
        ClientCapabilities clientCapabilities) =>
        new()
        {
            DocumentSelector = DashSpecLspHelpers.DocumentSelector,
            TriggerCharacters = new[] { " ", ".", "\"", "/" },
        };

    public Task<CompletionList> Handle(CompletionParams request, CancellationToken cancellationToken)
    {
        var document = request.TextDocument;
        var position = request.Position;
        var path = DashSpecLspHelpers.ToPath(document.Uri);
        var text = _state.GetDocumentText(path);
        if (text is null && File.Exists(path))
        {
            text = File.ReadAllText(path);
        }

        if (string.IsNullOrEmpty(text))
        {
            return Task.FromResult(new CompletionList());
        }

        var lines = text.Split('\n');
        if (position.Line < 0 || position.Line >= lines.Length)
        {
            return Task.FromResult(new CompletionList());
        }

        var line = lines[position.Line].TrimEnd('\r');
        var context = DashSpecLspHelpers.GetCompletionContext(line, position.Character);
        var items = context?.Kind switch
        {
            DashSpecLspHelpers.CompletionKind.Diagram => BuildIdItems(
                _state.Index.Diagrams.Keys,
                context.Value.Prefix,
                CompletionItemKind.Reference),
            DashSpecLspHelpers.CompletionKind.Presentation => BuildIdItems(
                _state.Index.Presentations.Keys,
                context.Value.Prefix,
                CompletionItemKind.Color),
            DashSpecLspHelpers.CompletionKind.Include => BuildIncludeItems(path, context.Value.Prefix),
            _ => BuildKeywordItems(line, position.Character),
        };

        return Task.FromResult(new CompletionList(items));
    }

    public Task<CompletionItem> Handle(CompletionItem item, CancellationToken cancellationToken) =>
        Task.FromResult(item);

    private static List<CompletionItem> BuildIdItems(
        IEnumerable<string> ids,
        string prefix,
        CompletionItemKind kind)
    {
        return ids
            .Where(id => id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .Select(id => new CompletionItem
            {
                Label = id,
                Kind = kind,
                InsertText = id,
            })
            .ToList();
    }

    private static List<CompletionItem> BuildIncludeItems(string filePath, string partial)
    {
        var directory = DashSpecLspHelpers.GetSpecDirectory(filePath);
        return IncludePathCompletion.Suggest(directory, partial)
            .Select(s => new CompletionItem
            {
                Label = s,
                Kind = s.EndsWith('/') ? CompletionItemKind.Folder : CompletionItemKind.File,
                InsertText = s,
            })
            .ToList();
    }

    private static List<CompletionItem> BuildKeywordItems(string line, int character)
    {
        var before = line[..Math.Clamp(character, 0, line.Length)];
        var wordStart = before.Length;
        while (wordStart > 0 && (char.IsLetterOrDigit(before[wordStart - 1]) || before[wordStart - 1] is '_' or '@' or '!'))
        {
            wordStart--;
        }

        var prefix = before[wordStart..];
        return Keywords
            .Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(k => new CompletionItem
            {
                Label = k,
                Kind = k.StartsWith('@') ? CompletionItemKind.Class : CompletionItemKind.Keyword,
                InsertText = k,
            })
            .ToList();
    }
}
