namespace DashSpec.Modeling.Parse.Diagram

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing

module DiagramParser =

    let private bannedTooltipPropertyKeys =
        set [ "tooltip"; "tooltip_time"; "tooltip_format"; "tooltip_split"; "tooltip_as" ]

    let parseAfterKindIdent (reader: TokenReader) (name: string) =
        match DiagramKindRegistry.tryResolve name with
        | true, spec when name.Equals("table", StringComparison.OrdinalIgnoreCase) ->
            let properties = TableDiagramParser.parse reader
            { Kind = name; Properties = properties :> IReadOnlyDictionary<_, _>; UsePreset = None }
        | true, spec ->
            let properties =
                PropertyBlockParser.parse
                    reader
                    (DiagramKindRegistry.getProperties name)
                    $"diagram {name}"
                    spec.AllowExtensionProperties
                    false
            { Kind = name; Properties = properties :> IReadOnlyDictionary<_, _>; UsePreset = None }
        | false, _ ->
            let overrides =
                if reader.IsOnNewline() || reader.IsEof then
                    Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) :> IReadOnlyDictionary<_, _>
                else
                    PropertyBlockParser.parse
                        reader
                        (DiagramKindRegistry.allBindingProperties ())
                        $"diagram {name}"
                        true
                        false
            { Kind = ""; Properties = overrides; UsePreset = Some name }

    let parse (reader: TokenReader) =
        let name = reader.ReadIdent()
        parseAfterKindIdent reader name
