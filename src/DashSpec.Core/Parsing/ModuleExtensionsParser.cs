using DashSpec.Core.Model;

namespace DashSpec.Core.Parsing;

internal static class ModuleExtensionsParser
{
    public static ModuleExtensionsDefinition Parse(TokenReader reader)
    {
        reader.Expect(TokenKind.LBrace);
        reader.SkipNewlines();

        var enabled = new List<string>();
        var imports = new List<ModuleExtensionImport>();

        while (!reader.IsAt(TokenKind.RBrace) && !reader.IsEof)
        {
            reader.SkipNewlines();
            if (reader.IsAt(TokenKind.RBrace))
            {
                break;
            }

            if (reader.TryKeyword("use"))
            {
                enabled.Add(reader.ReadIdent());
                reader.SkipNewlines();
                continue;
            }

            if (reader.TryKeyword("extension") || reader.TryKeyword("import"))
            {
                var pluginId = reader.ReadIdent();
                if (reader.TryKeyword("import") || reader.TryKeyword("from"))
                {
                    if (!reader.TryKeyword("from"))
                    {
                        reader.ExpectKeyword("from");
                    }

                    var path = reader.ReadString();
                    imports.Add(new ModuleExtensionImport(pluginId, path));
                }
                else
                {
                    enabled.Add(pluginId);
                }

                reader.SkipNewlines();
                continue;
            }

            throw reader.Unexpected("use, import, or extension");
        }

        reader.Expect(TokenKind.RBrace);
        return new ModuleExtensionsDefinition(enabled, imports);
    }
}
