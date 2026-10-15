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
    | Tabs

/// <summary>Single registry for import kind keywords — change kinds here only.</summary>
module ImportKindRegistry =

    type ImportKindDefinition =
        { Kind: ImportKind
          /// Import keyword: `import <keyword> from …`.
          Keyword: string
          /// Unit file keyword: `module <module-keyword> <id>`.
          ModuleKeyword: string }

    let private definitions: ImportKindDefinition[] =
        [|
            { Kind = ImportKind.Diagrams; Keyword = "diagrams"; ModuleKeyword = "diagram" }
            { Kind = ImportKind.Layouts; Keyword = "layouts"; ModuleKeyword = "layout" }
            { Kind = ImportKind.Presentations; Keyword = "presentations"; ModuleKeyword = "presentation" }
            { Kind = ImportKind.Tooltips; Keyword = "tooltips"; ModuleKeyword = "tooltip" }
            { Kind = ImportKind.Types; Keyword = "types"; ModuleKeyword = "types" }
            { Kind = ImportKind.Flows; Keyword = "flows"; ModuleKeyword = "flow" }
            { Kind = ImportKind.Palettes; Keyword = "palettes"; ModuleKeyword = "palette" }
            { Kind = ImportKind.Tabs; Keyword = "tabs"; ModuleKeyword = "tab" }
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

    /// Unit file keyword of the kind, as written after <c>module</c>.
    let moduleKeyword (kind: ImportKind) =
        definitions
        |> Array.find (fun d -> d.Kind = kind)
        |> fun d -> d.ModuleKeyword

    let tryFindByModuleKeyword (keyword: string) =
        definitions
        |> Array.tryFind (fun d -> String.Equals(d.ModuleKeyword, keyword, StringComparison.OrdinalIgnoreCase))
        |> Option.map (fun d -> d.Kind)
