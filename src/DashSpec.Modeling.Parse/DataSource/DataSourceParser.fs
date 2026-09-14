namespace DashSpec.Modeling.Parse.DataSource

open System
open System.IO
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing

module DataSourceParser =

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
            { Kind = DataSourceKind.Sql; Value = body; SqlCarrier = Some DataSourceSqlCarrier.Query }
        elif reader.TryKeyword "file" then
            let path = readSqlFileReference reader
            validateSqlFileExists path specDirectory
            { Kind = DataSourceKind.Sql; Value = path; SqlCarrier = Some DataSourceSqlCarrier.File }
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
                parsed <- Some { Kind = DataSourceKind.Sql; Value = body; SqlCarrier = Some DataSourceSqlCarrier.Query }
            elif reader.TryKeyword "file" then
                let path = readSqlFileReference reader
                validateSqlFileExists path specDirectory
                parsed <- Some { Kind = DataSourceKind.Sql; Value = path; SqlCarrier = Some DataSourceSqlCarrier.File }
            else
                raise (reader.Unexpected "query or file after from")
            reader.SkipNewlines()

        reader.Expect TokenKind.RBrace

        match parsed with
        | Some value -> value
        | None -> raise (DashSpecParseException("datasource sql { } requires from query or from file."))

    let parse (reader: TokenReader) (specDirectory: string option) =
        if reader.TryKeyword "view" then
            let name = reader.ReadQualifiedName()
            SqlReadOnlyValidator.validateViewReference name
            { Kind = DataSourceKind.View; Value = name; SqlCarrier = None }
        elif reader.TryKeyword "sql" then
            if reader.IsAt TokenKind.LBrace then parseSqlBlock reader specDirectory
            else parseSqlInline reader specDirectory
        else
            raise (reader.Unexpected "view or sql")
