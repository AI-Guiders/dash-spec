namespace DashSpec.Modeling.Parse.Catalog

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing
open DashSpec.Modeling.Parse.Syntax

module CatalogParser =

    let private unquote (raw: string) =
        let t = raw.Trim()
        if t.Length >= 2 && t.[0] = '"' && t.[t.Length - 1] = '"' then
            t.Substring(1, t.Length - 2)
        else
            t

    let rec private projectScopeEntries
        (members: DashSpecAstNode[])
        (groupId: string option)
        (entries: ResizeArray<CatalogEntryDefinition>)
        (groups: ResizeArray<CatalogGroupDefinition>)
        : string option
        =
        let mutable defaultEntryId: string option = None

        let rec parseFlatTokens (tokens: SyntaxToken list) =
            match tokens with
            | [] -> ()
            | { LexKind = TokenKind.Ident; Text = kw } :: rest
                when String.Equals(kw, "default", StringComparison.OrdinalIgnoreCase) ->
                if groupId.IsSome then
                    raise (DashSpecParseException("Catalog 'default' is only allowed at module scope."))
                match rest with
                | { LexKind = TokenKind.Ident; Text = id } :: tail when not (String.IsNullOrWhiteSpace id) ->
                    defaultEntryId <- Some id
                    parseFlatTokens tail
                | _ ->
                    raise (DashSpecParseException("Catalog default requires an entry id."))
            | { LexKind = TokenKind.Ident; Text = kw } :: rest
                when String.Equals(kw, "entry", StringComparison.OrdinalIgnoreCase) ->
                let id, afterId =
                    match rest with
                    | { LexKind = TokenKind.Ident; Text = entryId } :: tail when not (String.IsNullOrWhiteSpace entryId) ->
                        entryId, tail
                    | _ ->
                        raise (DashSpecParseException("Catalog entry requires an id."))

                let title, afterTitle =
                    match afterId with
                    | { LexKind = TokenKind.Ident; Text = asKw } :: { LexKind = TokenKind.String; Text = rawTitle } :: tail
                        when String.Equals(asKw, "as", StringComparison.OrdinalIgnoreCase) ->
                        unquote rawTitle, tail
                    | _ ->
                        id, afterId

                let dashspecPath, afterDashspec =
                    match afterTitle with
                    | { LexKind = TokenKind.Ident; Text = dsKw } :: { LexKind = TokenKind.String; Text = rawPath } :: tail
                        when String.Equals(dsKw, "dashspec", StringComparison.OrdinalIgnoreCase) ->
                        let p = unquote rawPath
                        if String.IsNullOrWhiteSpace p then
                            raise (DashSpecParseException($"Catalog entry '{id}' requires dashspec path."))
                        p, tail
                    | _ ->
                        raise (DashSpecParseException($"Catalog entry '{id}' requires dashspec path."))

                let initialTab, afterTab =
                    match afterDashspec with
                    | { LexKind = TokenKind.Ident; Text = secKw } :: tail
                        when String.Equals(secKw, "section", StringComparison.OrdinalIgnoreCase) ->
                        match tail with
                        | { LexKind = TokenKind.Ident; Text = tabId } :: restTail when not (String.IsNullOrWhiteSpace tabId) ->
                            Some tabId, restTail
                        | _ ->
                            raise (DashSpecParseException($"Catalog entry '{id}': tab requires an id."))
                    | _ ->
                        None, afterDashspec

                let initialPage, afterPage =
                    match afterTab with
                    | { LexKind = TokenKind.Ident; Text = pageKw } :: tail
                        when String.Equals(pageKw, "page", StringComparison.OrdinalIgnoreCase) ->
                        match tail with
                        | { LexKind = TokenKind.Ident; Text = pageId } :: restTail when not (String.IsNullOrWhiteSpace pageId) ->
                            Some pageId, restTail
                        | _ ->
                            raise (DashSpecParseException($"Catalog entry '{id}': page requires an id."))
                    | _ ->
                        None, afterTab

                entries.Add(
                    { Id = id
                      Title = title
                      DashspecPath = dashspecPath
                      GroupId = groupId
                      InitialTabId = initialTab
                      InitialPageId = initialPage })

                parseFlatTokens afterPage
            | tok :: _ ->
                raise (DashSpecParseException($"Unexpected token '{tok.Text}' in catalog."))

        let pendingTokens = ResizeArray<SyntaxToken>()

        let flushPending () =
            if pendingTokens.Count > 0 then
                let toks = List.ofSeq pendingTokens
                pendingTokens.Clear()
                parseFlatTokens toks

        let rec collectNode (node: DashSpecAstNode) =
            match node with
            | DashSpecAstNode.BlockDeclaration b ->
                match b.Opener with
                | DashSpecBlockOpener.Named(DashSpecBlockKeyword.Group, gId) ->
                    flushPending ()
                    if groupId.IsSome then
                        raise (DashSpecParseException("Nested groups are not supported in catalog."))
                    if groups |> Seq.exists (fun g -> String.Equals(g.Id, gId, StringComparison.OrdinalIgnoreCase)) then
                        raise (DashSpecParseException($"Catalog declares duplicate group id '{gId}'."))

                    let mutable groupTitle = gId
                    let innerMembers = ResizeArray<DashSpecAstNode>()

                    for child in (UnitGrammar.view b.Members).Body do
                        match child with
                        | DashSpecAstNode.PropertyAssignment p when String.Equals(p.Key, "title", StringComparison.OrdinalIgnoreCase) ->
                            groupTitle <- unquote p.ValueText
                        | other ->
                            innerMembers.Add other

                    projectScopeEntries (innerMembers.ToArray()) (Some gId) entries groups |> ignore
                    groups.Add({ Id = gId; Title = groupTitle })
                | DashSpecBlockOpener.Named(DashSpecBlockKeyword.Tab, _)
                | DashSpecBlockOpener.Named(DashSpecBlockKeyword.Page, _) ->
                    for tok in b.Tokens do
                        if tok.LexKind <> TokenKind.LineComment && tok.LexKind <> TokenKind.BlockComment then
                            pendingTokens.Add tok
                    for child in (UnitGrammar.view b.Members).Body do
                        collectNode child
                | _ ->
                    flushPending ()
                    let headerText = b.Tokens |> Array.map (fun t -> t.Text) |> String.concat " "
                    raise (DashSpecParseException($"Unexpected block '{headerText}' in catalog."))
            | other ->
                for tok in DashSpecAst.tokens other do
                    if tok.LexKind <> TokenKind.LineComment && tok.LexKind <> TokenKind.BlockComment then
                        pendingTokens.Add tok

        for node in (UnitGrammar.view members).Body do
            collectNode node

        flushPending ()
        defaultEntryId

    let parse (text: string) : CatalogDocument =
        if String.IsNullOrWhiteSpace text then invalidArg "text" "Catalog text is required."
        let tree = DashSpecSurfaceSyntax.parse text
        let env = UnitGrammar.unwrap DashSpecModuleKind.Catalog tree.Root
        let catalogId = env.Id

        let entries = ResizeArray<CatalogEntryDefinition>()
        let groups = ResizeArray<CatalogGroupDefinition>()

        let defaultEntryId = projectScopeEntries env.Body None entries groups

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
