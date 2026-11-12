namespace DashSpec.Modeling.Core

open System
open System.IO
open System.Reflection

/// <summary>Module <c>import &lt;kind&gt; from …</c> partition (ADR-0098). Loaded from <c>stdlib/units/units.ds</c>.</summary>
[<RequireQualifiedAccess>]
type ImportKind =
    | Diagrams
    | Layouts
    | Presentations
    | Tooltips
    | Types
    | Flows
    | Palettes
    | Tabs

[<RequireQualifiedAccess>]
type UnitManifestRole =
    | Declaration
    | Consumer
    | Root

type UnitManifestEntry =
    { UnitKeyword: string
      ImportKeyword: string option
      ImportKind: ImportKind option
      Role: UnitManifestRole }

/// <summary>Single registry for unit and import keywords loaded from embedded <c>stdlib/units/units.ds</c>.</summary>
module ImportKindRegistry =

    type ImportKindDefinition =
        { Kind: ImportKind
          /// Import keyword: `import <keyword> from …`.
          Keyword: string
          /// Unit file keyword: `module <module-keyword> <id>`.
          ModuleKeyword: string }

    let private tryMapImportKind (unitKw: string) =
        match unitKw.ToLowerInvariant() with
        | "diagram" -> Some ImportKind.Diagrams
        | "layout" -> Some ImportKind.Layouts
        | "presentation" -> Some ImportKind.Presentations
        | "tooltip" -> Some ImportKind.Tooltips
        | "types" -> Some ImportKind.Types
        | "flow" -> Some ImportKind.Flows
        | "palette" -> Some ImportKind.Palettes
        | "section" -> Some ImportKind.Tabs
        | _ -> None

    let private parseRole (raw: string) =
        match raw.Trim().ToLowerInvariant() with
        | "declaration" -> UnitManifestRole.Declaration
        | "root" -> UnitManifestRole.Root
        | _ -> UnitManifestRole.Consumer

    let unitManifest : UnitManifestEntry[] =
        let assembly = Assembly.GetExecutingAssembly()
        use stream = assembly.GetManifestResourceStream("stdlib/units/units.ds")
        if isNull stream then
            [||]
        else
            use reader = new StreamReader(stream)
            let lines =
                reader.ReadToEnd().Split([| '\r'; '\n' |], StringSplitOptions.RemoveEmptyEntries)
                |> Array.map (fun l -> l.Trim())
                |> Array.filter (fun l -> l.StartsWith('|'))

            if lines.Length <= 1 then
                [||]
            else
                lines.[1..]
                |> Array.choose (fun line ->
                    let cells = line.Split('|', StringSplitOptions.RemoveEmptyEntries ||| StringSplitOptions.TrimEntries)
                    if cells.Length >= 3 && not (cells.[0] |> Seq.forall (fun c -> c = '-')) then
                        let unitKw = cells.[0].Trim()
                        let impKw = if cells.[1] = "-" || String.IsNullOrWhiteSpace cells.[1] then None else Some(cells.[1].Trim())
                        Some
                            { UnitKeyword = unitKw
                              ImportKeyword = impKw
                              ImportKind = tryMapImportKind unitKw
                              Role = parseRole cells.[2] }
                    else
                        None)

    let private definitions: ImportKindDefinition[] =
        unitManifest
        |> Array.choose (fun entry ->
            match entry.ImportKind, entry.ImportKeyword with
            | Some kind, Some impKw ->
                Some
                    { Kind = kind
                      Keyword = impKw
                      ModuleKeyword = entry.UnitKeyword }
            | _ -> None)

    let allDefinitions = definitions

    let allKinds = definitions |> Array.map (fun d -> d.Kind) |> Array.distinct

    let isUnitKeyword (keyword: string) =
        not (String.IsNullOrWhiteSpace keyword)
        && unitManifest |> Array.exists (fun e -> String.Equals(e.UnitKeyword, keyword.Trim(), StringComparison.OrdinalIgnoreCase))

    let tryFindUnitEntry (unitKeyword: string) =
        unitManifest
        |> Array.tryFind (fun e -> String.Equals(e.UnitKeyword, unitKeyword, StringComparison.OrdinalIgnoreCase))

    let tryFindByKeyword (keyword: string) =
        definitions
        |> Array.tryFind (fun d -> String.Equals(d.Keyword, keyword, StringComparison.OrdinalIgnoreCase))
        |> Option.map (fun d -> d.Kind)

    let keyword (kind: ImportKind) =
        definitions
        |> Array.find (fun d -> d.Kind = kind)
        |> fun d -> d.Keyword

    /// Unit file keyword of the kind, as written after <c>module</c>.
    let moduleKeyword (kind: ImportKind) =
        definitions
        |> Array.find (fun d -> d.Kind = kind)
        |> fun d -> d.ModuleKeyword

    let tryFindByModuleKeyword (keyword: string) =
        definitions
        |> Array.tryFind (fun d -> String.Equals(d.ModuleKeyword, keyword, StringComparison.OrdinalIgnoreCase))
        |> Option.map (fun d -> d.Kind)
