using DashSpec.Execution.Compilation;
using DashSpec.Modeling.Language.Adapters.DashSpec;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace DashSpec.LanguageServer.Handlers;

internal sealed class DashSpecReferencesHandler(DashSpecServerState state) : IReferencesHandler
{
    public ReferenceRegistrationOptions GetRegistrationOptions(
        ReferenceCapability capability,
        ClientCapabilities clientCapabilities) =>
        new()
        {
            DocumentSelector = DashSpecLspHelpers.DocumentSelector,
        };

    public Task<LocationContainer?> Handle(
        ReferenceParams request,
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
            return Task.FromResult<LocationContainer?>(null);
        }

        var line = request.Position.Line + 1;
        var col = request.Position.Character + 1;

        var symOpt = DashSpecSymbolIndex.resolveSymbolAt(path, text, line, col);
        if (!Microsoft.FSharp.Core.FSharpOption<DashSpecSymbolIndex.SymbolDefinition>.get_IsSome(symOpt))
        {
            return Task.FromResult<LocationContainer?>(null);
        }

        var symbol = symOpt.Value;
        var workspaceUsages = DashSpecEditorIntelligence.FindWorkspaceUsages(symbol.Name, state.Index);
        if (workspaceUsages.Count > 0)
        {
            var locations = workspaceUsages
                .Select(u => new Location
                {
                    Uri = DashSpecLspHelpers.ToUri(u.Path),
                    Range = new LspRange(
                        new Position(Math.Max(0, u.Line - 1), Math.Max(0, u.Column - 1)),
                        new Position(Math.Max(0, u.EndLine - 1), Math.Max(0, u.EndColumn - 1))),
                })
                .ToList();
            return Task.FromResult<LocationContainer?>(new LocationContainer(locations));
        }

        var localRefs = DashSpecSymbolIndex.findReferences(path, text, line, col);
        var localLocations = localRefs
            .Select(r => new Location
            {
                Uri = DashSpecLspHelpers.ToUri(path),
                Range = new LspRange(
                    new Position(Math.Max(0, r.Span.Line - 1), Math.Max(0, r.Span.Column - 1)),
                    new Position(Math.Max(0, r.Span.EndLine - 1), Math.Max(0, r.Span.EndColumn - 1))),
            })
            .ToList();

        return Task.FromResult<LocationContainer?>(new LocationContainer(localLocations));
    }
}
