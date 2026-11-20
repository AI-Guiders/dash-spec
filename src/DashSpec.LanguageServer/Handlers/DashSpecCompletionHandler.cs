using DashSpec.Execution.Compilation;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace DashSpec.LanguageServer.Handlers;

internal sealed class DashSpecCompletionHandler(DashSpecServerState state) : ICompletionHandler
{
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
        var path = DashSpecLspHelpers.ToPath(request.TextDocument.Uri);
        var text = state.GetDocumentText(path);
        if (text is null && File.Exists(path))
        {
            text = File.ReadAllText(path);
        }

        if (string.IsNullOrEmpty(text))
        {
            return Task.FromResult(new CompletionList());
        }

        var suggestions = DashSpecEditorIntelligence.GetCompletions(
            path,
            text,
            request.Position.Line + 1,
            request.Position.Character,
            state.Index);

        var items = suggestions
            .Select(s => new CompletionItem
            {
                Label = s.Label,
                InsertText = s.InsertText,
                Detail = s.Detail,
                Kind = s.Kind switch
                {
                    "diagram" or "flow" or "layout" => CompletionItemKind.Reference,
                    "presentation" or "palette" => CompletionItemKind.Color,
                    "namespace" => CompletionItemKind.Module,
                    "class" => CompletionItemKind.Class,
                    _ => CompletionItemKind.Keyword,
                },
            })
            .ToList();

        return Task.FromResult(new CompletionList(items));
    }

#pragma warning disable VSTHRD200
    public Task<CompletionItem> Handle(CompletionItem item, CancellationToken cancellationToken) =>
        Task.FromResult(item);
#pragma warning restore VSTHRD200
}
