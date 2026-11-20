using AIGuiders.Platform.Modeling.Language;
using DashSpec.Modeling.Language.Adapters.DashSpec;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;
using LspTextEdit = OmniSharp.Extensions.LanguageServer.Protocol.Models.TextEdit;

namespace DashSpec.LanguageServer.Handlers;

internal sealed class DashSpecRenameHandler(DashSpecServerState state) : IRenameHandler
{
    private readonly ILanguageBackend _backend = new DashSpecLanguageBackend();

    public RenameRegistrationOptions GetRegistrationOptions(
        RenameCapability capability,
        ClientCapabilities clientCapabilities) =>
        new()
        {
            DocumentSelector = DashSpecLspHelpers.DocumentSelector,
        };

    public async Task<WorkspaceEdit?> Handle(
        RenameParams request,
        CancellationToken cancellationToken)
    {
        var path = DashSpecLspHelpers.ToPath(request.TextDocument.Uri);
        var text = state.GetDocumentText(path);
        if (text is null && File.Exists(path))
        {
            text = await File.ReadAllTextAsync(path, cancellationToken);
        }

        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var langReq = new LanguageRequest(
            path,
            request.Position.Line + 1,
            request.Position.Character + 1,
            text,
            DashSpecLspHelpers.GetSpecDirectory(path),
            LanguageIds.Dashspec);

        var result = await _backend.RenameSymbolAsync(
            new RenameSymbolRequest(langReq, request.NewName, false),
            cancellationToken);

        if (result.Changes.Length == 0)
        {
            return null;
        }

        var lines = text.Split('\n');
        var changes = new Dictionary<DocumentUri, IEnumerable<LspTextEdit>>
        {
            [request.TextDocument.Uri] =
            [
                new LspTextEdit
                {
                    Range = new LspRange(new Position(0, 0), new Position(lines.Length, 0)),
                    NewText = result.Changes[0].NewText,
                },
            ],
        };

        return new WorkspaceEdit { Changes = changes };
    }
}
