namespace DashSpec.Modeling.Parse.DataSource

open System
open System.Text
open System.Text.RegularExpressions
open DashSpec.Modeling.Core

/// Validates datasource sql text and view qualified names at parse time.
module SqlReadOnlyValidator =

    let private forbiddenKeywords =
        [| "INSERT"; "UPDATE"; "DELETE"; "MERGE"; "TRUNCATE"
           "DROP"; "ALTER"; "CREATE"; "EXEC"; "EXECUTE"
           "GRANT"; "REVOKE"; "DENY"; "BACKUP"; "RESTORE"
           "SHUTDOWN"; "DBCC"; "CALL" |]

    let private viewReferencePattern = Regex(@"^[\w]+(\.[\w]+)*$", RegexOptions.CultureInvariant)
    let private blockCommentPattern = Regex(@"/\*", RegexOptions.CultureInvariant)
    let private lineCommentPattern = Regex(@"--", RegexOptions.CultureInvariant)
    let private selectIntoPattern = Regex(@"\bSELECT\b[\s\S]*\bINTO\b", RegexOptions.IgnoreCase ||| RegexOptions.CultureInvariant)
    let private dangerousTokenPattern = Regex(@"\b(OPENROWSET|OPENDATASOURCE|xp_\w+|sp_executesql|BULK\s+INSERT)\b", RegexOptions.IgnoreCase ||| RegexOptions.CultureInvariant)

    let private containsWholeWord (text: string) (word: string) =
        Regex.IsMatch(text, $@"\b{Regex.Escape word}\b", RegexOptions.IgnoreCase ||| RegexOptions.CultureInvariant)

    let private startsWithReadQuery (text: string) =
        let trimmed = text.TrimStart()
        trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
        || trimmed.StartsWith("WITH", StringComparison.OrdinalIgnoreCase)

    let private skipQuoted (sql: string) (start: int) (quote: char) =
        let rec loop i =
            if i >= sql.Length then sql.Length
            elif sql.[i] = '\\' && i + 1 < sql.Length then loop (i + 2)
            elif sql.[i] = quote then
                if i + 1 < sql.Length && sql.[i + 1] = quote then loop (i + 2)
                else i + 1
            else loop (i + 1)

        loop (start + 1)

    let private skipNQuoted (sql: string) (start: int) =
        let rec loop i =
            if i >= sql.Length then sql.Length
            elif sql.[i] = '\'' && i + 1 < sql.Length && sql.[i + 1] = '\'' then loop (i + 2)
            elif sql.[i] = '\'' then i + 1
            else loop (i + 1)

        loop (start + 2)

    let private stripStringLiterals (sql: string) =
        let sb = StringBuilder(sql.Length)
        let mutable i = 0

        while i < sql.Length do
            if sql.[i] = '\'' || sql.[i] = '"' then
                let quote = sql.[i]
                i <- skipQuoted sql i quote
                sb.Append(' ') |> ignore
            elif i + 1 < sql.Length && (sql.[i] = 'N' || sql.[i] = 'n') && sql.[i + 1] = '\'' then
                i <- skipNQuoted sql i
                sb.Append(' ') |> ignore
            else
                sb.Append(sql.[i]) |> ignore
                i <- i + 1

        sb.ToString()

    let validateViewReference (qualifiedName: string) =
        if String.IsNullOrWhiteSpace qualifiedName then
            raise (ArgumentException("qualifiedName"))

        if not (viewReferencePattern.IsMatch qualifiedName) then
            raise (DashSpecParseException($"datasource view must be a qualified name (schema.object), got '{qualifiedName}'."))

    let validateSqlBody (sql: string) =
        let body = sql.Trim()

        if body.Length = 0 then
            raise (DashSpecParseException("datasource sql must not be empty."))

        if body.Contains(';') then
            raise (DashSpecParseException("datasource sql must be a single read-only SELECT (semicolons are not allowed)."))

        if blockCommentPattern.IsMatch body || lineCommentPattern.IsMatch body then
            raise (DashSpecParseException("datasource sql must not contain SQL comments (-- or /* */)."))

        let scanTarget = stripStringLiterals body

        if not (startsWithReadQuery scanTarget) then
            raise (DashSpecParseException("datasource sql must start with SELECT or WITH (read-only query)."))

        if selectIntoPattern.IsMatch scanTarget then
            raise (DashSpecParseException("datasource sql must not use SELECT INTO."))

        for keyword in forbiddenKeywords do
            if containsWholeWord scanTarget keyword then
                raise (DashSpecParseException($"datasource sql must be read-only; forbidden keyword '{keyword}'."))

        if dangerousTokenPattern.IsMatch scanTarget then
            raise (DashSpecParseException("datasource sql contains a disallowed token (e.g. OPENROWSET, xp_, sp_executesql)."))
