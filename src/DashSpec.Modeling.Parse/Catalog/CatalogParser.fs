namespace DashSpec.Modeling.Parse.Catalog

open System
open System.Collections.Generic
open System.Linq
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing

module CatalogParser =

    let private parseEntry (reader: TokenReader) (groupId: string option) =
        let id = reader.ReadIdent()
        if String.IsNullOrWhiteSpace id then
            raise (DashSpecParseException("Catalog entry requires an id."))
        let mutable title = id
        if reader.TryKeyword "as" then
            title <- reader.ReadString()
        reader.ExpectKeyword "dashspec"
        let dashspecPath = reader.ReadString()
        if String.IsNullOrWhiteSpace dashspecPath then
            raise (DashSpecParseException($"Catalog entry '{id}' requires dashspec path."))
        reader.SkipNewlines()
        { Id = id; Title = title; DashspecPath = dashspecPath; GroupId = groupId }

    let private parseGroup (reader: TokenReader) (entries: ResizeArray<CatalogEntryDefinition>) (groups: ResizeArray<CatalogGroupDefinition>) =
        let groupId = reader.ReadIdent()
        if String.IsNullOrWhiteSpace groupId then
            raise (DashSpecParseException("Catalog group requires an id."))
        if groups |> Seq.exists (fun g -> String.Equals(g.Id, groupId, StringComparison.OrdinalIgnoreCase)) then
            raise (DashSpecParseException($"Catalog declares duplicate group id '{groupId}'."))
        BlockSyntax.beginBlock reader
        reader.SkipNewlines()
        let mutable groupTitle = groupId
        while not (BlockSyntax.isBlockEnd reader "group" (Some groupId)) && not reader.IsEof do
            reader.SkipNewlines()
            if BlockSyntax.isBlockEnd reader "group" (Some groupId) then ()
            elif reader.TryKeyword "title" then
                reader.Expect TokenKind.Eq
                groupTitle <- reader.ReadString()
                reader.SkipNewlines()
            elif reader.TryKeyword "entry" then
                entries.Add(parseEntry reader (Some groupId))
            else
                raise (reader.Unexpected())
        BlockSyntax.expectBlockEnd reader "group" (Some groupId)
        groups.Add({ Id = groupId; Title = groupTitle })
        reader.SkipNewlines()

    let parse (text: string) : CatalogDocument =
        if String.IsNullOrWhiteSpace text then invalidArg "text" "Catalog text is required."
        let reader = ParserUtilities.createReader text
        reader.SkipNewlines()
        reader.Expect TokenKind.At
        reader.ExpectKeyword "catalog"
        let catalogId = reader.ReadIdent()
        if String.IsNullOrWhiteSpace catalogId then
            raise (DashSpecParseException("Catalog module requires @catalog <id>."))
        reader.SkipNewlines()
        let mutable defaultEntryId: string option = None
        let entries = ResizeArray<CatalogEntryDefinition>()
        let groups = ResizeArray<CatalogGroupDefinition>()
        while not reader.IsEof do
            if reader.TryKeyword "default" then
                let id = reader.ReadIdent()
                if String.IsNullOrWhiteSpace id then
                    raise (DashSpecParseException("Catalog default requires an entry id."))
                defaultEntryId <- Some id
                reader.SkipNewlines()
            elif reader.TryKeyword "group" then
                parseGroup reader entries groups
            elif reader.TryKeyword "entry" then
                entries.Add(parseEntry reader None)
            else
                raise (reader.Unexpected())
        if entries.Count = 0 then
            raise (DashSpecParseException($"Catalog '{catalogId}' must declare at least one entry."))
        let defaultId = defaultEntryId |> Option.defaultValue entries.[0].Id
        if entries |> Seq.forall (fun e -> not (String.Equals(e.Id, defaultId, StringComparison.OrdinalIgnoreCase))) then
            raise (DashSpecParseException($"Catalog '{catalogId}': default entry '{defaultId}' not found."))
        let dup = entries |> Seq.groupBy (fun e -> e.Id) |> Seq.tryFind (fun (_, g) -> Seq.length g > 1)
        match dup with
        | Some (key, _) -> raise (DashSpecParseException($"Catalog '{catalogId}': duplicate entry id '{key}'."))
        | None -> ()
        let groupsOpt = if groups.Count = 0 then None else Some(groups :> IReadOnlyList<_>)
        { Id = catalogId; DefaultEntryId = defaultId; Entries = entries :> IReadOnlyList<_>; Groups = groupsOpt }