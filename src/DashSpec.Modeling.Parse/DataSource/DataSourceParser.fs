namespace DashSpec.Modeling.Parse.DataSource

open System
open System.IO
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing

module DataSourceParser =

    let private requireProviderInfer (reader: TokenReader) =
        reader.SkipNewlines()

        if not (reader.TryKeyword "infer") then
            raise (
                DashSpecParseException(
                    "datasource requires 'infer' (manifest default_provider_id; same as dashflow 'use provider infer')."))

    let private readRowsType (reader: TokenReader) =
        reader.SkipNewlines()
        if not (reader.TryKeyword "rows") then
            raise (DashSpecParseException("datasource requires rows <RowType> (Modeling SSOT; ADR-0087 B2)."))
        let typeName = reader.ReadIdent()
        if String.IsNullOrWhiteSpace typeName then
            raise (DashSpecParseException("datasource rows requires a type name."))
        typeName

    let private withRowsType (reader: TokenReader) (def: DataSourceDefinition) =
        { def with RowsType = readRowsType reader }

    let private inferred (kind: DataSourceKind) (value: string) (sqlCarrier: DataSourceSqlCarrier option) (sheet: string option) =
        { Kind = kind
          Value = value
          SqlCarrier = sqlCarrier
          Sheet = sheet
          RowsType = ""
          ProviderInfer = true }

    let private unwrapRawSql (raw: string) =
        let trimmed = raw.Trim()
        if trimmed.StartsWith("[[", StringComparison.Ordinal) && trimmed.EndsWith("]]", StringComparison.Ordinal) then
            trimmed.Substring(2, trimmed.Length - 4).Trim()
        else
            trimmed

    let private readSqlQueryText (reader: TokenReader) =
        reader.SkipNewlines()
        match reader.CurrentKind with
        | TokenKind.String -> reader.ReadString()
        | TokenKind.Raw -> unwrapRawSql (reader.ReadRawBlock())
        | _ -> raise (reader.Unexpected "SQL query string or [[ … ]] block")

    let private readSqlFileReference (reader: TokenReader) =
        reader.SkipNewlines()
        if reader.CurrentKind <> TokenKind.String then
            raise (reader.Unexpected "file path string")
        let path = reader.ReadString()
        if String.IsNullOrWhiteSpace path then
            raise (DashSpecParseException("datasource sql file path must not be empty."))
        path.Replace('\\', '/')

    let private validateSqlFileExists (relativePath: string) (specDirectory: string option) =
        match specDirectory with
        | None | Some (null | "") -> ()
        | Some dir ->
            let path = Path.GetFullPath(Path.Combine(dir, relativePath))
            if not (File.Exists path) then
                raise (FileNotFoundException($"datasource sql file not found: '{relativePath}' (resolved: {path}).", path))

    let private parseSqlInline (reader: TokenReader) (specDirectory: string option) =
        if reader.TryKeyword "query" then
            let body = readSqlQueryText reader
            SqlReadOnlyValidator.validateSqlBody body
            withRowsType reader (inferred DataSourceKind.Sql body (Some DataSourceSqlCarrier.Query) None)
        elif reader.TryKeyword "file" then
            let path = readSqlFileReference reader
            validateSqlFileExists path specDirectory
            withRowsType reader (inferred DataSourceKind.Sql path (Some DataSourceSqlCarrier.File) None)
        else
            raise (DashSpecParseException("datasource sql requires 'query' or 'file' (e.g. datasource sql query \"SELECT …\" or datasource sql file \"sql/x.sql\")."))

    let private parseSqlBlock (reader: TokenReader) (specDirectory: string option) =
        reader.Expect TokenKind.LBrace
        reader.SkipNewlines()
        let mutable parsed: DataSourceDefinition option = None

        while not (reader.IsAt TokenKind.RBrace) && not reader.IsEof do
            reader.SkipNewlines()
            if reader.IsAt TokenKind.RBrace then ()
            elif not (reader.TryKeyword "from") then
                raise (reader.Unexpected "from")
            elif reader.TryKeyword "query" then
                let body = readSqlQueryText reader
                SqlReadOnlyValidator.validateSqlBody body
                parsed <- Some(inferred DataSourceKind.Sql body (Some DataSourceSqlCarrier.Query) None)
            elif reader.TryKeyword "file" then
                let path = readSqlFileReference reader
                validateSqlFileExists path specDirectory
                parsed <- Some(inferred DataSourceKind.Sql path (Some DataSourceSqlCarrier.File) None)
            else
                raise (reader.Unexpected "query or file after from")
            reader.SkipNewlines()

        reader.Expect TokenKind.RBrace

        match parsed with
        | Some value -> withRowsType reader value
        | None -> raise (DashSpecParseException("datasource sql { } requires from query or from file."))

    let private parseXlsxFile (reader: TokenReader) (specDirectory: string option) =
        if not (reader.TryKeyword "file") then
            raise (DashSpecParseException("datasource xlsx requires 'file' (e.g. datasource xlsx file \"data/book.xlsx\" sheet \"Sheet1\")."))
        let path = readSqlFileReference reader
        if not (path.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)) then
            raise (DashSpecParseException("datasource xlsx file must end with .xlsx."))
        validateSqlFileExists path specDirectory
        reader.SkipNewlines()
        let sheet =
            if reader.TryKeyword "sheet" then
                reader.SkipNewlines()
                if reader.CurrentKind <> TokenKind.String then
                    raise (reader.Unexpected "sheet name string")
                let name = reader.ReadString().Trim()
                if String.IsNullOrWhiteSpace name then
                    raise (DashSpecParseException("datasource xlsx sheet name must not be empty."))
                Some name
            else
                None
        withRowsType reader (inferred DataSourceKind.Xlsx path None sheet)

    let parse (reader: TokenReader) (specDirectory: string option) =
        requireProviderInfer reader

        if reader.TryKeyword "view" then
            let name = AccessorGrammar.readQualifiedName reader
            SqlReadOnlyValidator.validateViewReference name
            withRowsType reader (inferred DataSourceKind.View name None None)
        elif reader.TryKeyword "sql" then
            if reader.IsAt TokenKind.LBrace then parseSqlBlock reader specDirectory
            else parseSqlInline reader specDirectory
        elif reader.TryKeyword "xlsx" then
            parseXlsxFile reader specDirectory
        else
            raise (reader.Unexpected "view, sql, or xlsx")
