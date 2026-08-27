using DashSpec.Core.Model;
using DashSpec.Core.Runtime;

namespace DashSpec.Core.Parsing;

internal static class TooltipModuleParser
{
    public static TooltipDefinition ParseTooltipFile(string text) =>
        ParseTooltipFileWithId(text).Definition;

    public static (string Id, TooltipDefinition Definition) ParseTooltipFileWithId(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var reader = ParserUtilities.CreateReader(text);
        reader.SkipFileDirectives();
        reader.Expect(TokenKind.At);
        reader.ExpectKeyword("tooltip");
        var id = reader.ReadIdent();
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new DashSpecParseException("@tooltip module requires @tooltip <id>.");
        }

        reader.SkipNewlines();
        return (id, ParseBody(reader, id));
    }

    public static TooltipDefinition ParseInline(TokenReader reader, string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return ParseBody(reader, id);
    }

    private static TooltipDefinition ParseBody(TokenReader reader, string id)
    {
        Dictionary<string, string>? variables = null;
        string? template = null;
        string? source = null;

        while (!reader.IsEof)
        {
            reader.SkipNewlines();
            if (reader.IsEof)
            {
                break;
            }

            if (reader.TryKeyword("variables"))
            {
                if (variables is not null)
                {
                    throw new DashSpecParseException($"Tooltip '{id}': duplicate variables block.");
                }

                variables = ParseVariables(reader, id);
                continue;
            }

            if (reader.TryKeyword("tooltip"))
            {
                reader.Expect(TokenKind.Eq);
                template = reader.ReadString();
                continue;
            }

            if (reader.TryKeyword("source"))
            {
                reader.Expect(TokenKind.Eq);
                source = reader.ReadIdent();
                if (string.IsNullOrWhiteSpace(source))
                {
                    throw new DashSpecParseException($"Tooltip '{id}': source requires a column name.");
                }

                continue;
            }

            if (reader.TryKeyword("end") && reader.TryKeyword("tooltip"))
            {
                break;
            }

            // Flat @tooltip file: no trailing end required; stop on unexpected.
            if (!reader.IsEof)
            {
                throw reader.Unexpected();
            }
        }

        if (!string.IsNullOrWhiteSpace(source))
        {
            if (variables is not null || template is not null)
            {
                throw new DashSpecParseException(
                    $"Tooltip '{id}': source cannot be combined with variables/tooltip.");
            }

            variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["value"] = source,
            };
            template = "{value}";
        }

        if (variables is null || variables.Count == 0)
        {
            throw new DashSpecParseException($"Tooltip '{id}': variables (or source) is required.");
        }

        if (string.IsNullOrWhiteSpace(template))
        {
            throw new DashSpecParseException($"Tooltip '{id}': tooltip = \"...\" is required.");
        }

        var definition = new TooltipDefinition(id, variables, template);
        try
        {
            TooltipTemplate.Validate(definition);
        }
        catch (InvalidOperationException ex)
        {
            throw new DashSpecParseException(ex.Message);
        }

        return definition;
    }

    private static Dictionary<string, string> ParseVariables(TokenReader reader, string tooltipId)
    {
        BlockSyntax.BeginBlock(reader);
        reader.SkipNewlines();

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        while (!BlockSyntax.IsBlockEnd(reader, "variables") && !reader.IsEof)
        {
            reader.SkipNewlines();
            if (BlockSyntax.IsBlockEnd(reader, "variables"))
            {
                break;
            }

            var name = reader.ReadIdent();
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new DashSpecParseException($"Tooltip '{tooltipId}': variables entry requires a name.");
            }

            reader.Expect(TokenKind.Eq);
            var column = reader.ReadIdent();
            if (string.IsNullOrWhiteSpace(column))
            {
                throw new DashSpecParseException(
                    $"Tooltip '{tooltipId}': variables '{name}' requires a column.");
            }

            if (!map.TryAdd(name, column))
            {
                throw new DashSpecParseException(
                    $"Tooltip '{tooltipId}': duplicate variable '{name}'.");
            }

            reader.SkipNewlines();
        }

        BlockSyntax.ExpectBlockEnd(reader, "variables");
        return map;
    }
}
