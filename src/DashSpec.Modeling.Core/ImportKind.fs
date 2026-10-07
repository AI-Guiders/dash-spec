namespace DashSpec.Modeling.Core

open System

/// <summary>Module <c>import &lt;kind&gt; from …</c> partition (ADR-0098). Extend via <see cref="ImportKindRegistry"/>.</summary>
[<RequireQualifiedAccess>]
type ImportKind =
    | Diagrams
    | Layouts
    | Presentations
    | Tooltips
    | Types
    | Flows
    | Palettes

/// <summary>Single registry for import kind keywords — change kinds here only.</summary>
module ImportKindRegistry =

    type ImportKindDefinition =
        { Kind: ImportKind
          Keyword: string }

    let private definitions: ImportKindDefinition[] =
        [|
            { Kind = ImportKind.Diagrams; Keyword = "diagrams" }
            { Kind = ImportKind.Layouts; Keyword = "layouts" }
            { Kind = ImportKind.Presentations; Keyword = "presentations" }
            { Kind = ImportKind.Tooltips; Keyword = "tooltips" }
            { Kind = ImportKind.Types; Keyword = "types" }
            { Kind = ImportKind.Flows; Keyword = "flows" }
            { Kind = ImportKind.Palettes; Keyword = "palettes" }
        |]

    let allDefinitions = definitions

    let allKinds = definitions |> Array.map (fun d -> d.Kind)

    let tryFindByKeyword (keyword: string) =
        definitions
        |> Array.tryFind (fun d -> String.Equals(d.Keyword, keyword, StringComparison.OrdinalIgnoreCase))
        |> Option.map (fun d -> d.Kind)

    let keyword (kind: ImportKind) =
        definitions
        |> Array.find (fun d -> d.Kind = kind)
        |> fun d -> d.Keyword
