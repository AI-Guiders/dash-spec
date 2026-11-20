using DashSpec.Execution.Compilation;
using DashSpec.Modeling.Language.Adapters.DashSpec;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace DashSpec.LanguageServer.Handlers;

internal sealed class DashSpecDefinitionHandler(DashSpecServerState state) : IDefinitionHandler
{
    public DefinitionRegistrationOptions GetRegistrationOptions(
        DefinitionCapability capability,
        ClientCapabilities clientCapabilities) =>
        new()
        {
            DocumentSelector = DashSpecLspHelpers.DocumentSelector,
        };

    public Task<LocationOrLocationLinks?> Handle(
        DefinitionParams request,
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
            return Task.FromResult<LocationOrLocationLinks?>(null);
        }

        var line = request.Position.Line + 1;
        var col = request.Position.Character + 1;

        var workspaceTarget = DashSpecEditorIntelligence.TryGetDefinition(
            path,
            text,
            line,
            col,
            state.Index);

        if (workspaceTarget is not null && File.Exists(workspaceTarget.FilePath))
        {
            var targetLine = Math.Max(0, workspaceTarget.Line - 1);
            var targetCol = Math.Max(0, workspaceTarget.Column - 1);
            var location = new Location
            {
                Uri = DashSpecLspHelpers.ToUri(workspaceTarget.FilePath),
                Range = new LspRange(new Position(targetLine, targetCol), new Position(targetLine, targetCol)),
            };
            return Task.FromResult<LocationOrLocationLinks?>(new LocationOrLocationLinks(location));
        }

        var localSpanOpt = DashSpecSymbolIndex.goToDefinition(path, text, line, col);
        if (Microsoft.FSharp.Core.FSharpOption<AIGuiders.Platform.Modeling.Language.SourceSpan>.get_IsSome(localSpanOpt))
        {
            var span = localSpanOpt.Value;
            var location = new Location
            {
                Uri = DashSpecLspHelpers.ToUri(path),
                Range = new LspRange(
                    new Position(Math.Max(0, span.Line - 1), Math.Max(0, span.Column - 1)),
                    new Position(Math.Max(0, span.EndLine - 1), Math.Max(0, span.EndColumn - 1))),
            };
            return Task.FromResult<LocationOrLocationLinks?>(new LocationOrLocationLinks(location));
        }

        return Task.FromResult<LocationOrLocationLinks?>(null);
    }
}
