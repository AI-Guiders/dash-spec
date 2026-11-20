using DashSpec.Modeling.Parse.Formatting;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace DashSpec.LanguageServer.Handlers;

internal sealed class DashSpecFormattingHandler(DashSpecServerState state) : IDocumentFormattingHandler
{
    public DocumentFormattingRegistrationOptions GetRegistrationOptions(
        DocumentFormattingCapability capability,
        ClientCapabilities clientCapabilities) =>
        new()
        {
            DocumentSelector = DashSpecLspHelpers.DocumentSelector,
        };

    public Task<TextEditContainer?> Handle(
        DocumentFormattingParams request,
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
            return Task.FromResult<TextEditContainer?>(null);
        }

        var options = new DashSpecFormatOptions
        {
            IndentStyle = request.Options.InsertSpaces ? "space" : "tab",
            IndentSize = (int)Math.Clamp(request.Options.TabSize, 1, 8),
            EndOfLine = "lf",
            TrimTrailingWhitespace = true,
            InsertFinalNewline = true,
            DashSpecFormatOnSave = true,
            DashSpecMaxConsecutiveBlankLines = 1,
            DashSpecIndentBlockBody = true,
            DashSpecBlankLineBetweenBlocks = true,
            DashSpecPreserveBlankLineBeforeEnd = false,
        };

        var formatted = DashSpecBlockFormatter.format(text, options);
        if (string.Equals(text, formatted, StringComparison.Ordinal))
        {
            return Task.FromResult<TextEditContainer?>(new TextEditContainer());
        }

        var lines = text.Split('\n');
        var edit = new TextEdit
        {
            Range = new LspRange(
                new Position(0, 0),
                new Position(lines.Length, 0)),
            NewText = formatted,
        };

        return Task.FromResult<TextEditContainer?>(new TextEditContainer(edit));
    }
}
