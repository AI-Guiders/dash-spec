using DashSpec.Core.Model;



namespace DashSpec.Core.Parsing;



public static class CatalogParser

{

    public static CatalogDocument Parse(string text)

    {

        ArgumentException.ThrowIfNullOrWhiteSpace(text);



        var reader = ParserUtilities.CreateReader(text);

        reader.SkipNewlines();

        reader.Expect(TokenKind.At);

        reader.ExpectKeyword("catalog");

        var catalogId = reader.ReadIdent();

        if (string.IsNullOrWhiteSpace(catalogId))

        {

            throw new DashSpecParseException("Catalog module requires @catalog <id>.");

        }



        reader.SkipNewlines();

        string? defaultEntryId = null;

        var entries = new List<CatalogEntryDefinition>();

        var groups = new List<CatalogGroupDefinition>();



        while (!reader.IsEof)

        {

            if (reader.TryKeyword("default"))

            {

                defaultEntryId = reader.ReadIdent();

                if (string.IsNullOrWhiteSpace(defaultEntryId))

                {

                    throw new DashSpecParseException("Catalog default requires an entry id.");

                }



                reader.SkipNewlines();

                continue;

            }



            if (reader.TryKeyword("group"))

            {

                ParseGroup(reader, entries, groups);

                continue;

            }



            if (reader.TryKeyword("entry"))

            {

                entries.Add(ParseEntry(reader));

                continue;

            }



            throw reader.Unexpected();

        }



        if (entries.Count == 0)

        {

            throw new DashSpecParseException($"Catalog '{catalogId}' must declare at least one entry.");

        }



        defaultEntryId ??= entries[0].Id;

        if (entries.All(e => !string.Equals(e.Id, defaultEntryId, StringComparison.OrdinalIgnoreCase)))

        {

            throw new DashSpecParseException(

                $"Catalog '{catalogId}': default entry '{defaultEntryId}' not found.");

        }



        var duplicate = entries

            .GroupBy(e => e.Id, StringComparer.OrdinalIgnoreCase)

            .FirstOrDefault(g => g.Count() > 1);

        if (duplicate is not null)

        {

            throw new DashSpecParseException(

                $"Catalog '{catalogId}': duplicate entry id '{duplicate.Key}'.");

        }



        return new CatalogDocument(catalogId, defaultEntryId, entries, groups);

    }



    public static CatalogDocument ParseFile(string path)

    {

        if (!File.Exists(path))

        {

            throw new FileNotFoundException("Catalog file not found.", path);

        }



        return Parse(File.ReadAllText(path));

    }



    public static string ResolveEntrySpecPath(string catalogFullPath, string dashspecReference)

    {

        ArgumentException.ThrowIfNullOrWhiteSpace(catalogFullPath);

        ArgumentException.ThrowIfNullOrWhiteSpace(dashspecReference);



        var catalogDir = Path.GetDirectoryName(catalogFullPath)!;

        var searchDirs = new[]

        {

            catalogDir,

            Path.GetFullPath(Path.Combine(catalogDir, "..")),

        };



        foreach (var dir in searchDirs.Distinct(StringComparer.OrdinalIgnoreCase))

        {

            var path = SpecIncludeResolver.ResolvePath(dashspecReference, dir);

            if (!path.EndsWith(".dashspec", StringComparison.OrdinalIgnoreCase))

            {

                path += ".dashspec";

            }



            if (File.Exists(path))

            {

                return path;

            }

        }



        throw new FileNotFoundException(

            $"Catalog entry dashspec not found: '{dashspecReference}' (catalog: {catalogFullPath}).",

            dashspecReference);

    }



    private static void ParseGroup(

        TokenReader reader,

        List<CatalogEntryDefinition> entries,

        List<CatalogGroupDefinition> groups)

    {

        var groupId = reader.ReadIdent();

        if (string.IsNullOrWhiteSpace(groupId))

        {

            throw new DashSpecParseException("Catalog group requires an id.");

        }



        if (groups.Any(group => string.Equals(group.Id, groupId, StringComparison.OrdinalIgnoreCase)))

        {

            throw new DashSpecParseException($"Catalog declares duplicate group id '{groupId}'.");

        }



        BlockSyntax.BeginBlock(reader);
        reader.SkipNewlines();

        var groupTitle = groupId;
        while (!BlockSyntax.IsBlockEnd(reader, "group", groupId) && !reader.IsEof)
        {
            reader.SkipNewlines();
            if (BlockSyntax.IsBlockEnd(reader, "group", groupId))
            {
                break;
            }

            if (reader.TryKeyword("title"))
            {
                reader.Expect(TokenKind.Eq);
                groupTitle = reader.ReadString();
                reader.SkipNewlines();
                continue;
            }

            if (reader.TryKeyword("entry"))
            {
                entries.Add(ParseEntry(reader, groupId));
                continue;
            }

            throw reader.Unexpected();
        }

        BlockSyntax.ExpectBlockEnd(reader, "group", groupId);

        groups.Add(new CatalogGroupDefinition(groupId, groupTitle));

        reader.SkipNewlines();

    }



    private static CatalogEntryDefinition ParseEntry(TokenReader reader, string? groupId = null)

    {

        var id = reader.ReadIdent();

        if (string.IsNullOrWhiteSpace(id))

        {

            throw new DashSpecParseException("Catalog entry requires an id.");

        }



        string title = id;

        if (reader.TryKeyword("as"))

        {

            title = reader.ReadString();

        }



        reader.ExpectKeyword("dashspec");

        var dashspecPath = reader.ReadString();

        if (string.IsNullOrWhiteSpace(dashspecPath))

        {

            throw new DashSpecParseException($"Catalog entry '{id}' requires dashspec path.");

        }



        reader.SkipNewlines();

        return new CatalogEntryDefinition(id, title, dashspecPath, groupId);

    }

}


