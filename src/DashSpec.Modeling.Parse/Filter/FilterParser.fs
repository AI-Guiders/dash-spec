namespace DashSpec.Modeling.Parse.Filter

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing

/// Filter declaration grammar (ADR-0010 legacy kind-first; ADR-0037 structured id-first bind/show).
module FilterParser =

    let private defaultsHint (filterName: string) =
        $"defaults block: filter.{filterName} = …"

    let private rejectInlineDefault (filterName: string) =
        raise (DashSpecParseException($"Filter '{filterName}': declare default in {defaultsHint filterName}."))

    type private StructuredBindParse =
        { Properties: Dictionary<string, string>
          GrainLabels: IReadOnlyDictionary<string, string> option }

    let private kindName kind =
        match kind with
        | FilterKind.Date -> "date"
        | FilterKind.Field -> "field"
        | FilterKind.Top -> "top"

    let private isFilterKind (value: string) =
        value.Equals("date", StringComparison.OrdinalIgnoreCase)
        || value.Equals("field", StringComparison.OrdinalIgnoreCase)
        || value.Equals("top", StringComparison.OrdinalIgnoreCase)

    let private parseFilterKindFromIdent (ident: string) =
        match ident.ToLowerInvariant() with
        | "date" -> FilterKind.Date
        | "field" -> FilterKind.Field
        | "top" -> FilterKind.Top
        | _ -> raise (DashSpecParseException($"Unknown filter kind '{ident}'."))

    let private readTypedValue (reader: TokenReader) (valueType: PropertySchemas.PropertyValueType) =
        match valueType with
        | PropertySchemas.PropertyValueType.Scalar -> reader.ReadScalarValue()
        | PropertySchemas.PropertyValueType.String -> reader.ReadString()
        | PropertySchemas.PropertyValueType.DateRange -> reader.ReadDateDefaultValue()
        | PropertySchemas.PropertyValueType.QualifiedName -> reader.ReadQualifiedName()
        | PropertySchemas.PropertyValueType.CommaList -> reader.ReadCommaSeparatedValues()
        | PropertySchemas.PropertyValueType.RestOfLine -> reader.ReadRestOfLine()
        | PropertySchemas.PropertyValueType.ColumnBinding -> invalidOp "ColumnBinding must be handled separately."

    let private resolveSingleSelect (widget: string option) (props: IReadOnlyDictionary<string, string>) =
        match widget with
        | Some w when w.Equals("select", StringComparison.OrdinalIgnoreCase) -> true
        | _ ->
            match props.TryGetValue "single" with
            | true, raw ->
                raw.Equals("true", StringComparison.OrdinalIgnoreCase)
                || raw.Equals("1", StringComparison.OrdinalIgnoreCase)
                || raw.Equals("yes", StringComparison.OrdinalIgnoreCase)
            | false, _ -> false

    let private validateSemantics
        (kind: FilterKind)
        (name: string)
        (defaultExpression: string option)
        (widget: string option)
        (columnReference: string option)
        (props: IReadOnlyDictionary<string, string>)
        (singleSelect: bool)
        =
        let mutable defaultExpr = defaultExpression
        let mutable minValue: int option = None
        let mutable maxValue: int option = None

        match kind with
        | FilterKind.Date ->
            if defaultExpr.IsNone || String.IsNullOrWhiteSpace defaultExpr.Value then
                raise (DashSpecParseException($"Date filter '{name}' requires {defaultsHint name} (range from..to, e.g. -7d..today)."))

            let mutable expr = defaultExpr.Value

            if widget.IsSome && widget.Value.Equals("day", StringComparison.OrdinalIgnoreCase) && not (expr.Contains "..") then
                expr <- $"{expr}..{expr}"
            elif not (expr.Contains "..") then
                raise (DashSpecParseException($"Date filter '{name}' default must be a range 'from..to', e.g. -7d..today"))

            DateDefaultRange.validateSyntax expr
            defaultExpr <- Some expr

            match widget with
            | Some w when w.Equals("day", StringComparison.OrdinalIgnoreCase) ->
                DateDefaultRange.validateSingleDayDefault expr
            | Some w when not (w.Equals("range", StringComparison.OrdinalIgnoreCase)) ->
                raise (DashSpecParseException($"Date filter '{name}' widget must be 'day' or 'range', got '{w}'."))
            | _ -> ()

            if columnReference.IsNone || String.IsNullOrWhiteSpace columnReference.Value then
                raise (DashSpecParseException($"Date filter '{name}' requires column in bind block or on <column> as \"Label\"."))

        | FilterKind.Field ->
            match widget with
            | Some w when
                not (w.Equals("combobox", StringComparison.OrdinalIgnoreCase))
                && not (w.Equals("chips", StringComparison.OrdinalIgnoreCase))
                && not (w.Equals("select", StringComparison.OrdinalIgnoreCase))
                ->
                raise (DashSpecParseException($"Field filter '{name}' widget must be 'combobox', 'chips', or 'select', got '{w}'."))
            | _ -> ()

            match defaultExpr with
            | Some expr when singleSelect && expr.Contains ',' ->
                raise (DashSpecParseException($"Field filter '{name}' is single-select; default must be one value, got '{expr}'."))
            | _ -> ()

            if columnReference.IsNone || String.IsNullOrWhiteSpace columnReference.Value then
                raise (DashSpecParseException($"Field filter '{name}' requires column in bind block or on <column> as \"Label\"."))

        | FilterKind.Top ->
            match props.TryGetValue "min" with
            | true, minRaw ->
                match Int32.TryParse minRaw with
                | true, parsedMin when parsedMin >= 0 -> minValue <- Some parsedMin
                | _ -> raise (DashSpecParseException($"Top filter '{name}' min must be a non-negative integer (0 = no row cap)."))
            | false, _ -> ()

            match defaultExpr with
            | Some expr ->
                match Int32.TryParse expr with
                | true, defaultTop when defaultTop > 0 -> ()
                | true, 0 when minValue = Some 0 -> ()
                | _ ->
                    raise (DashSpecParseException($"Top filter '{name}' requires positive numeric default, or 0 when min = 0 (no row cap)."))
            | None ->
                raise (DashSpecParseException($"Top filter '{name}' requires numeric {defaultsHint name} (e.g. 200)."))

            match props.TryGetValue "max" with
            | true, maxRaw ->
                match Int32.TryParse maxRaw with
                | true, parsedMax when parsedMax > 0 -> maxValue <- Some parsedMax
                | _ -> raise (DashSpecParseException($"Top filter '{name}' max must be a positive integer."))
            | false, _ -> ()

            match minValue, maxValue with
            | Some minV, Some maxV when minV > maxV ->
                raise (DashSpecParseException($"Top filter '{name}' min cannot exceed max."))
            | _ -> ()

        defaultExpr, minValue, maxValue

    let private resolveStructuredLabel (name: string) (kind: FilterKind) (showProps: IReadOnlyDictionary<string, string>) =
        match kind with
        | FilterKind.Top | FilterKind.Date | FilterKind.Field ->
            match showProps.TryGetValue "label" with
            | true, label when not (String.IsNullOrWhiteSpace label) -> label
            | _ -> raise (DashSpecParseException($"Filter '{name}': show block requires label = \"…\"."))

    let private resolveFilterLabel
        (name: string)
        (kind: FilterKind)
        (props: IReadOnlyDictionary<string, string>)
        (declarationLabel: string option)
        (labelFromOn: string option)
        =
        match kind with
        | FilterKind.Top ->
            match declarationLabel with
            | Some label when not (String.IsNullOrWhiteSpace label) -> label
            | _ -> raise (DashSpecParseException($"Top filter '{name}' requires as \"Label\"."))
        | _ ->
            match labelFromOn with
            | Some label when not (String.IsNullOrWhiteSpace label) -> label
            | _ ->
                match declarationLabel with
                | Some _ ->
                    raise (DashSpecParseException($"Filter '{name}' uses on <column> as \"Label\", not as on the filter line."))
                | None ->
                    match props.TryGetValue "column_as" with
                    | true, columnLabel when not (String.IsNullOrWhiteSpace columnLabel) -> columnLabel
                    | _ ->
                        raise (DashSpecParseException($"Filter '{name}' requires on <column> as \"Label\" or column = … as \"Label\" in {{ }}."))

    let private parseBindKind (reader: TokenReader) (filterName: string) =
        match reader.TryPeekIdent() with
        | None -> raise (reader.Unexpected "bind kind (date, field, or top)")
        | Some kindIdent when not (isFilterKind kindIdent) ->
            raise (DashSpecParseException($"Filter '{filterName}': bind requires date, field, or top, got '{kindIdent}'."))
        | Some _ -> parseFilterKindFromIdent (reader.ReadIdent())

    let private parseStructuredBindBlock (reader: TokenReader) (kind: FilterKind) (name: string) =
        let schema =
            match kind with
            | FilterKind.Date -> PropertySchemas.filterBindDate
            | FilterKind.Field -> PropertySchemas.filterBindField
            | FilterKind.Top -> PropertySchemas.filterTop

        let blockName = $"filter {name} bind {kindName kind}"
        BlockSyntax.beginBlock reader
        reader.SkipNewlines()

        let specs =
            schema
            |> List.map (fun s -> s.Name, s)
            |> dict

        let values = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        let mutable grainLabels: IReadOnlyDictionary<string, string> option = None

        while not (BlockSyntax.isBlockEnd reader "bind" None) && not reader.IsEof do
            reader.SkipNewlines()

            if BlockSyntax.isBlockEnd reader "bind" None then ()
            else
                while not (reader.IsOnNewline()) && not (BlockSyntax.isBlockEnd reader "bind" None) && not reader.IsEof do
                    if BlockSyntax.isBlockEnd reader "bind" None then ()
                    else
                        let key = reader.ReadPropertyKey(allowQuoted=false)

                        if key.Equals("labels", StringComparison.OrdinalIgnoreCase) then
                            if kind <> FilterKind.Date then
                                raise (DashSpecParseException($"{blockName}: labels block is allowed only on date filters."))

                            if grainLabels.IsSome then
                                raise (DashSpecParseException($"{blockName}: duplicate labels block."))

                            reader.SkipNewlines()
                            grainLabels <- Some(PropertyBlockParser.parseStringMapBlock reader "labels" $"{blockName} labels")
                        elif key.Equals("default", StringComparison.OrdinalIgnoreCase) then
                            rejectInlineDefault name
                        elif specs.ContainsKey key then
                            let spec = specs.[key]
                            reader.Expect TokenKind.Eq

                            if spec.ValueType = PropertySchemas.PropertyValueType.ColumnBinding then
                                let binding = reader.ReadColumnBinding()
                                values.[key] <- binding.Column

                                match binding.Alias with
                                | Some alias -> values.[key + "_as"] <- alias
                                | None -> ()
                            else
                                values.[key] <- readTypedValue reader spec.ValueType
                        else
                            raise (DashSpecParseException($"Unknown property '{key}' in {blockName} block."))

                reader.SkipNewlines()

        BlockSyntax.expectBlockEnd reader "bind" None
        { Properties = values; GrainLabels = grainLabels }

    let private parseStructuredIdFirst (reader: TokenReader) (name: string) (resolveDefault: string -> string option) =
        let mutable kind: FilterKind option = None
        let bindProps = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        let showProps = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        let mutable grainLabels: IReadOnlyDictionary<string, string> option = None

        BlockSyntax.beginBlock reader
        reader.SkipNewlines()

        while not (BlockSyntax.isBlockEnd reader "filter" None) && not reader.IsEof do
            reader.SkipNewlines()

            if BlockSyntax.isBlockEnd reader "filter" None then ()
            elif reader.TryKeyword "bind" then
                if kind.IsSome then
                    raise (DashSpecParseException($"Filter '{name}': duplicate bind block."))

                let parsedKind = parseBindKind reader name
                kind <- Some parsedKind
                let parsed = parseStructuredBindBlock reader parsedKind name

                for kv in parsed.Properties do
                    bindProps.[kv.Key] <- kv.Value

                grainLabels <- parsed.GrainLabels
            elif reader.TryKeyword "show" then
                if showProps.Count > 0 then
                    raise (DashSpecParseException($"Filter '{name}': duplicate show block."))

                let props =
                    PropertyBlockParser.parseWithEndKind
                        reader
                        PropertySchemas.filterShow
                        $"filter {name} show"
                        "show"
                        false
                        false

                for kv in props do
                    showProps.[kv.Key] <- kv.Value
            else
                raise (reader.Unexpected "bind or show")

        BlockSyntax.expectBlockEnd reader "filter" None

        match kind with
        | None -> raise (DashSpecParseException($"Filter '{name}': bind block is required."))
        | Some resolvedKind ->
            if bindProps.ContainsKey "default" then
                rejectInlineDefault name

            let defaultExpression = resolveDefault name

            let columnReference =
                match bindProps.TryGetValue "column" with
                | true, value -> Some value
                | false, _ -> None

            let widget =
                match showProps.TryGetValue "widget" with
                | true, value -> Some value
                | false, _ -> None

            let grainFilterName =
                match showProps.TryGetValue "grain_filter" with
                | true, value -> Some value
                | false, _ ->
                    match bindProps.TryGetValue "grain_filter" with
                    | true, value -> Some value
                    | false, _ -> None

            let layoutRef =
                match showProps.TryGetValue "ref" with
                | true, value -> Some value
                | false, _ -> None

            let label = resolveStructuredLabel name resolvedKind showProps
            let singleSelect = resolveSingleSelect widget bindProps
            let defaultExpression, minValue, maxValue =
                validateSemantics resolvedKind name defaultExpression widget columnReference bindProps singleSelect

            { Kind = resolvedKind
              Name = name
              DefaultExpression = defaultExpression
              ColumnReference = columnReference
              Label = Some label
              Widget = widget
              MinValue = minValue
              MaxValue = maxValue
              GrainFilterName = grainFilterName
              SingleSelect = singleSelect
              LayoutRef = layoutRef
              GrainLabels = grainLabels }

    let private tryParseTopLabel (reader: TokenReader) (kind: FilterKind) =
        if kind <> FilterKind.Top || not (reader.TryKeyword "as") then
            None
        else
            Some(reader.ReadString())

    let private tryParseOnBinding (reader: TokenReader) (kind: FilterKind) =
        if kind <> FilterKind.Date && kind <> FilterKind.Field then
            None, None
        elif not (reader.TryKeywordSameLine "on") then
            None, None
        else
            let binding = reader.ReadColumnBinding()
            Some binding.Column, binding.Alias

    let private hasInlineProperties (reader: TokenReader) =
        if reader.IsOnNewline() then false
        else
            match reader.RawKind with
            | TokenKind.Eof | TokenKind.RBrace | TokenKind.LBrace -> false
            | TokenKind.Ident -> true
            | _ -> false

    let private looksLikeFilterPropertyContinuation (reader: TokenReader) =
        reader.SkipNewlines()

        if reader.IsEof || BlockSyntax.isBlockEnd reader "filter" None then
            false
        elif reader.RawKind <> TokenKind.Ident then
            false
        else
            match reader.TryPeekIdent() with
            | None -> false
            | Some key when key.Equals("end", StringComparison.OrdinalIgnoreCase) -> false
            | Some key ->
                key.Equals("column", StringComparison.OrdinalIgnoreCase)
                || key.Equals("widget", StringComparison.OrdinalIgnoreCase)
                || key.Equals("grain_filter", StringComparison.OrdinalIgnoreCase)
                || key.Equals("single", StringComparison.OrdinalIgnoreCase)
                || key.Equals("labels", StringComparison.OrdinalIgnoreCase)
                || key.Equals("min", StringComparison.OrdinalIgnoreCase)
                || key.Equals("max", StringComparison.OrdinalIgnoreCase)

    let private parseInlineProperties (reader: TokenReader) (kind: FilterKind) =
        let props = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)

        while hasInlineProperties reader do
            let key = reader.ReadIdent()

            if key.Equals("default", StringComparison.OrdinalIgnoreCase) then
                raise (DashSpecParseException("Filter default must be declared in defaults block: filter.<id> = …"))
            elif key.Equals("single", StringComparison.OrdinalIgnoreCase) && reader.RawKind <> TokenKind.Eq then
                props.[key] <- "true"
            else
                if reader.RawKind = TokenKind.Eq then
                    reader.Advance()

                props.[key] <- reader.ReadScalarValue()

        reader.SkipNewlines()
        props

    let private parsePropertyBlock
        (reader: TokenReader)
        (kind: FilterKind)
        (name: string)
        (columnProvidedByOn: bool)
        =
        let schema =
            match kind with
            | FilterKind.Date -> PropertySchemas.filterDate
            | FilterKind.Field -> PropertySchemas.filterField
            | FilterKind.Top -> PropertySchemas.filterTop

        let blockName = $"filter {kindName kind} {name}"
        BlockSyntax.beginBlock reader
        reader.SkipNewlines()

        let specs =
            schema
            |> List.map (fun s -> s.Name, s)
            |> dict

        let values = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        let mutable grainLabels: IReadOnlyDictionary<string, string> option = None

        while not (BlockSyntax.isBlockEnd reader "filter" None) && not reader.IsEof do
            reader.SkipNewlines()

            if BlockSyntax.isBlockEnd reader "filter" None then ()
            else
                while not (reader.IsAt TokenKind.Newline) && not (BlockSyntax.isBlockEnd reader "filter" None) && not reader.IsEof do
                    let key = reader.ReadPropertyKey(allowQuoted=false)

                    if key.Equals("labels", StringComparison.OrdinalIgnoreCase) then
                        if kind <> FilterKind.Date then
                            raise (DashSpecParseException($"{blockName}: labels block is allowed only on date filters."))

                        if grainLabels.IsSome then
                            raise (DashSpecParseException($"{blockName}: duplicate labels block."))

                        reader.SkipNewlines()
                        grainLabels <- Some(PropertyBlockParser.parseStringMapBlock reader "labels" $"{blockName} labels")
                    elif key.Equals("default", StringComparison.OrdinalIgnoreCase) then
                        rejectInlineDefault name
                    elif specs.ContainsKey key then
                        let spec = specs.[key]
                        reader.Expect TokenKind.Eq

                        if spec.ValueType = PropertySchemas.PropertyValueType.ColumnBinding then
                            let binding = reader.ReadColumnBinding()
                            values.[key] <- binding.Column

                            match binding.Alias with
                            | Some alias -> values.[key + "_as"] <- alias
                            | None -> ()
                        else
                            values.[key] <- readTypedValue reader spec.ValueType
                    else
                        raise (DashSpecParseException($"Unknown property '{key}' in {blockName} block."))

                reader.SkipNewlines()

        BlockSyntax.expectBlockEnd reader "filter" None

        if columnProvidedByOn then
            values.Remove "column" |> ignore
            values.Remove "column_as" |> ignore

        values, grainLabels

    let private parseFilterBody (reader: TokenReader) (kind: FilterKind) (name: string) (columnProvidedByOn: bool) =
        if reader.RawKind = TokenKind.LBrace then
            raise (DashSpecParseException($"Filter '{name}': brace bodies removed; use properties + end filter."))

        if hasInlineProperties reader then
            let props = parseInlineProperties reader kind
            reader.SkipNewlines()

            if not reader.IsEof && looksLikeFilterPropertyContinuation reader then
                let blockProps, grainLabels = parsePropertyBlock reader kind name columnProvidedByOn

                for kv in blockProps do
                    props.[kv.Key] <- kv.Value

                props, grainLabels
            else
                props, None
        else
            reader.SkipNewlines()

            if not reader.IsEof && looksLikeFilterPropertyContinuation reader then
                parsePropertyBlock reader kind name columnProvidedByOn
            else
                Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), None

    let private parseLegacyKindFirst (reader: TokenReader) (kind: FilterKind) (resolveDefault: string -> string option) =
        let name = reader.ReadIdentSameLine()
        let declarationLabel = tryParseTopLabel reader kind
        let columnFromOn, labelFromOn = tryParseOnBinding reader kind

        let trailingLabel =
            if labelFromOn.IsNone && (kind = FilterKind.Date || kind = FilterKind.Field) && reader.TryKeywordSameLine "as" then
                Some(reader.ReadString())
            else
                None

        let layoutRef = ParserUtilities.tryReadLayoutRef reader

        if reader.TryKeywordSameLine "default" then
            rejectInlineDefault name

        let props, grainLabels = parseFilterBody reader kind name columnFromOn.IsSome

        let columnReference =
            match columnFromOn with
            | Some column -> Some column
            | None ->
                match props.TryGetValue "column" with
                | true, value -> Some value
                | false, _ -> None

        let defaultExpression = resolveDefault name

        let label = resolveFilterLabel name kind props declarationLabel (labelFromOn |> Option.orElse trailingLabel)

        let widget =
            match props.TryGetValue "widget" with
            | true, value -> Some value
            | false, _ -> None

        let grainFilterName =
            match props.TryGetValue "grain_filter" with
            | true, value -> Some value
            | false, _ -> None

        let singleSelect = resolveSingleSelect widget props
        let defaultExpression, minValue, maxValue =
            validateSemantics kind name defaultExpression widget columnReference props singleSelect

        { Kind = kind
          Name = name
          DefaultExpression = defaultExpression
          ColumnReference = columnReference
          Label = Some label
          Widget = widget
          MinValue = minValue
          MaxValue = maxValue
          GrainFilterName = grainFilterName
          SingleSelect = singleSelect
          LayoutRef = layoutRef
          GrainLabels = grainLabels }

    let parse (reader: TokenReader) (resolveDefault: string -> string option) =
        match reader.TryPeekIdent() with
        | None -> raise (reader.Unexpected "filter id or kind (date, field, top)")
        | Some first when isFilterKind first ->
            parseLegacyKindFirst reader (parseFilterKindFromIdent (reader.ReadIdent())) resolveDefault
        | Some name ->
            reader.ReadIdent() |> ignore
            parseStructuredIdFirst reader name resolveDefault
