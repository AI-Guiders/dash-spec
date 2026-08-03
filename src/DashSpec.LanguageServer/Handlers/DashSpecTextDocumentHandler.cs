using DashSpec.LanguageServer;
using DashSpec.Core.Parsing;
using DashSpec.Core.Validation;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;

namespace DashSpec.LanguageServer.Handlers;

internal sealed class DashSpecTextDocumentHandler : TextDocumentSyncHandlerBase
{
    private readonly ILanguageServerFacade _facade;
    private readonly DashSpecServerState _state;

    public DashSpecTextDocumentHandler(ILanguageServerFacade facade, DashSpecServerState state)
    {
        _facade = facade;
        _state = state;
    }

    public TextDocumentSyncKind Change { get; } = TextDocumentSyncKind.Full;

    public override TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri) =>
        new(uri, "dashspec");

    protected override TextDocumentSyncRegistrationOptions CreateRegistrationOptions(
        TextSynchronizationCapability capability,
        ClientCapabilities clientCapabilities) =>
        new()
        {
            DocumentSelector = DashSpecLspHelpers.DocumentSelector,
            Change = Change,
            Save = new SaveOptions { IncludeText = true },
        };

    public override async Task<Unit> Handle(
        DidOpenTextDocumentParams request,
        CancellationToken cancellationToken)
    {
        var path = DashSpecLspHelpers.ToPath(request.TextDocument.Uri);
        _state.OpenDocument(path, request.TextDocument.Text);
        await PublishDiagnosticsAsync(request.TextDocument.Uri, path, request.TextDocument.Text);
        return Unit.Value;
    }

    public override async Task<Unit> Handle(
        DidChangeTextDocumentParams request,
        CancellationToken cancellationToken)
    {
        var path = DashSpecLspHelpers.ToPath(request.TextDocument.Uri);
        var text = request.ContentChanges.LastOrDefault()?.Text ?? string.Empty;
        _state.ChangeDocument(path, text);
        await PublishDiagnosticsAsync(request.TextDocument.Uri, path, text);
        return Unit.Value;
    }

    public override async Task<Unit> Handle(
        DidSaveTextDocumentParams request,
        CancellationToken cancellationToken)
    {
        var path = DashSpecLspHelpers.ToPath(request.TextDocument.Uri);
        var text = request.Text ?? _state.GetDocumentText(path) ?? await ReadFileAsync(path);
        if (text is not null)
        {
            _state.ChangeDocument(path, text);
            await PublishDiagnosticsAsync(request.TextDocument.Uri, path, text);
        }

        return Unit.Value;
    }

    public override Task<Unit> Handle(
        DidCloseTextDocumentParams request,
        CancellationToken cancellationToken)
    {
        _state.CloseDocument(DashSpecLspHelpers.ToPath(request.TextDocument.Uri));
        return Unit.Task;
    }

    private Task PublishDiagnosticsAsync(DocumentUri uri, string path, string text)
    {
        var specDirectory = DashSpecLspHelpers.GetSpecDirectory(path);
        var diagnostics = DashSpecDiagnosticService.ValidateText(
            text,
            path,
            specDirectory,
            DashSpecParseOptions.Editor);

        _facade.TextDocument.PublishDiagnostics(
            new PublishDiagnosticsParams
            {
                Uri = uri,
                Diagnostics = diagnostics.Select(DashSpecLspHelpers.ToLspDiagnostic).ToList(),
            });

        return Task.CompletedTask;
    }

    private static async Task<string?> ReadFileAsync(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        return await File.ReadAllTextAsync(path);
    }
}
