using DashSpec.LanguageServer;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace DashSpec.LanguageServer.Handlers;

internal sealed class DashSpecDefinitionHandler : IDefinitionHandler
{
    private readonly DashSpecServerState _state;

    public DashSpecDefinitionHandler(DashSpecServerState state) => _state = state;

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
        var text = _state.GetDocumentText(path);
        if (text is null && File.Exists(path))
        {
            text = File.ReadAllText(path);
        }

        if (string.IsNullOrEmpty(text))
        {
            return Task.FromResult<LocationOrLocationLinks?>(null);
        }

        var lines = text.Split('\n');
        if (request.Position.Line < 0 || request.Position.Line >= lines.Length)
        {
            return Task.FromResult<LocationOrLocationLinks?>(null);
        }

        var line = lines[request.Position.Line].TrimEnd('\r');
        var word = GetWordAt(line, request.Position.Character);
        if (string.IsNullOrWhiteSpace(word))
        {
            return Task.FromResult<LocationOrLocationLinks?>(null);
        }

        var kind = DashSpecLspHelpers.GetDefinitionKind(line, word);
        string? targetPath = kind switch
        {
            DashSpecLspHelpers.DefinitionKind.Diagram when _state.Index.Diagrams.TryGetValue(word, out var diagramPath)
                => diagramPath,
            DashSpecLspHelpers.DefinitionKind.Presentation when _state.Index.Presentations.TryGetValue(word, out var presentationPath)
                => presentationPath,
            DashSpecLspHelpers.DefinitionKind.Include => ResolveIncludePath(path, word),
            _ => null,
        };

        if (string.IsNullOrWhiteSpace(targetPath) || !File.Exists(targetPath))
        {
            return Task.FromResult<LocationOrLocationLinks?>(null);
        }

        var location = new Location
        {
            Uri = DashSpecLspHelpers.ToUri(targetPath),
            Range = new LspRange(new Position(0, 0), new Position(0, 0)),
        };

        return Task.FromResult<LocationOrLocationLinks?>(new LocationOrLocationLinks(location));
    }

    private static string? ResolveIncludePath(string filePath, string reference)
    {
        var directory = DashSpecLspHelpers.GetSpecDirectory(filePath);
        var combined = Path.GetFullPath(Path.Combine(directory, reference.Replace('/', Path.DirectorySeparatorChar)));
        if (File.Exists(combined))
        {
            return combined;
        }

        foreach (var ext in new[] { ".dashdiagram", ".dashpresentation", ".dashspec", ".dashinclude" })
        {
            var withExt = combined.EndsWith(ext, StringComparison.OrdinalIgnoreCase) ? combined : combined + ext;
            if (File.Exists(withExt))
            {
                return withExt;
            }
        }

        return null;
    }

    private static string GetWordAt(string line, int character)
    {
        if (string.IsNullOrEmpty(line))
        {
            return string.Empty;
        }

        var index = Math.Clamp(character, 0, line.Length);
        var start = index;
        while (start > 0 && IsIdentChar(line[start - 1]))
        {
            start--;
        }

        var end = index;
        while (end < line.Length && IsIdentChar(line[end]))
        {
            end++;
        }

        return line[start..end];
    }

    private static bool IsIdentChar(char ch) =>
        char.IsLetterOrDigit(ch) || ch is '_' or '-' or '.';
}
