using DashSpec.Modeling.Language.Adapters.DashSpec;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace DashSpec.LanguageServer.Handlers;

internal sealed class DashSpecDocumentSymbolHandler(DashSpecServerState state) : IDocumentSymbolHandler
{
    public DocumentSymbolRegistrationOptions GetRegistrationOptions(
        DocumentSymbolCapability capability,
        ClientCapabilities clientCapabilities) =>
        new()
        {
            DocumentSelector = DashSpecLspHelpers.DocumentSelector,
        };

    public Task<SymbolInformationOrDocumentSymbolContainer?> Handle(
        DocumentSymbolParams request,
        CancellationToken cancellationToken)
    {
        var path = DashSpecLspHelpers.ToPath(request.TextDocument.Uri);
        var text = state.GetDocumentText(path);
        if (text is null && File.Exists(path))
        {
            text = File.ReadAllText(path);
        }

        if (string.IsNullOrEmpty(text))
        {
            return Task.FromResult<SymbolInformationOrDocumentSymbolContainer?>(null);
        }

        var defs = DashSpecSymbolIndex.collectDefinitions(path, text);
        var symbols = defs
            .Select(d =>
            {
                var range = new LspRange(
                    new Position(Math.Max(0, d.Span.Line - 1), Math.Max(0, d.Span.Column - 1)),
                    new Position(Math.Max(0, d.Span.EndLine - 1), Math.Max(0, d.Span.EndColumn - 1)));
                return new SymbolInformationOrDocumentSymbol(new DocumentSymbol
                {
                    Name = d.Name,
                    Detail = d.Kind,
                    Kind = d.Kind switch
                    {
                        "dashboard" or "tab" or "section" or "catalog" => SymbolKind.Module,
                        "card" or "diagram" => SymbolKind.Class,
                        "filter" or "port" => SymbolKind.Property,
                        "source" or "transformer" or "flow" => SymbolKind.Function,
                        "type" => SymbolKind.Struct,
                        _ => SymbolKind.Object,
                    },
                    Range = range,
                    SelectionRange = range,
                });
            })
            .ToList();

        return Task.FromResult<SymbolInformationOrDocumentSymbolContainer?>(
            new SymbolInformationOrDocumentSymbolContainer(symbols));
    }
}
