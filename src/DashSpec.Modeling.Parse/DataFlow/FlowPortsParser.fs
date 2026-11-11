namespace DashSpec.Modeling.Parse.DataFlow

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.Syntax

/// <c>ports</c> … <c>end ports</c> — <c>input stream|scalar Name: Type</c>, <c>output stream Name: Type</c>.
module FlowPortsParser =

    [<RequireQualifiedAccess>]
    type PortShape =
        | Stream
        | Scalar

    type ParsedPortDecl =
        { Name: string
          ValueType: string
          Shape: PortShape }

    /// <summary><c>param name: type = default</c> — typed widget-value port (ADR-0120).</summary>
    type ParsedParamDecl =
        { Name: string
          ValueType: string
          Default: string option
          ColumnBinding: string option }

    type ParsedPorts =
        { Inputs: ParsedPortDecl[]
          Outputs: ParsedPortDecl[]
          Params: ParsedParamDecl[]
          DefaultInput: string option
          DefaultOutput: string option }

    let toDashPortType (decl: ParsedPortDecl) =
        match decl.Shape with
        | PortShape.Stream -> DashPortType.Stream decl.ValueType
        | PortShape.Scalar -> DashPortType.Scalar decl.ValueType

    let private validateDefaultPort (role: string) (name: string) (decls: ParsedPortDecl[]) =
        if String.IsNullOrWhiteSpace name then
            raise (DashSpecParseException($"default {role} requires a port name."))

        if not (decls |> Array.exists (fun d -> String.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase))) then
            raise (DashSpecParseException($"default {role} '{name}' is not declared in this ports block."))

    /// <summary>ADR-0130: project <see cref="ParsedPorts"/> directly from a typed
    /// ports-block AST (<c>DashSpecAstNode</c> members), raising <see cref="DashSpecParseException"/>
    /// on any malformed member.</summary>
    let projectPortsFromAst (members: DashSpecAstNode[]) : ParsedPorts =
        let inputs = ResizeArray<ParsedPortDecl>()
        let outputs = ResizeArray<ParsedPortDecl>()
        let paramsDecls = ResizeArray<ParsedParamDecl>()
        let mutable defaultInput = None
        let mutable defaultOutput = None

        for memberNode in members do
            match memberNode with
            | DashSpecAstNode.PortDeclaration decl ->
                let shape =
                    match decl.PortShape with
                    | Some PortShapeSyntax.Scalar -> PortShape.Scalar
                    | _ -> PortShape.Stream

                match decl.Role with
                | PortDeclRole.PortInput ->
                    inputs.Add { Name = decl.PortName; ValueType = decl.ValueType |> Option.defaultValue ""; Shape = shape }
                | PortDeclRole.PortOutput ->
                    if shape = PortShape.Scalar then
                        raise (DashSpecParseException("output ports must be stream (row batch)."))
                    outputs.Add { Name = decl.PortName; ValueType = decl.ValueType |> Option.defaultValue ""; Shape = shape }
                | PortDeclRole.PortParam ->
                    paramsDecls.Add
                        { Name = decl.PortName
                          ValueType = decl.ValueType |> Option.defaultValue ""
                          Default = decl.DefaultValue
                          ColumnBinding = decl.ColumnBinding }
            | DashSpecAstNode.ClauseDeclaration clause ->
                match clause.Kind with
                | ClauseKind.DefaultPortClause(direction, portName) ->
                    match direction with
                    | PortDirection.Input -> defaultInput <- Some portName
                    | PortDirection.Output -> defaultOutput <- Some portName
                | _ ->
                    let lineText = clause.Tokens |> Array.map (fun t -> t.Text) |> String.concat " "
                    raise (DashSpecParseException($"Unexpected clause '{lineText}' in ports block."))
            | DashSpecAstNode.BlankLine _
            | DashSpecAstNode.EndBlock _ -> ()
            | memberNode when DashSpecAst.isTrivia memberNode -> ()
            | other ->
                let lineText = DashSpecAst.tokens other |> Array.map (fun t -> t.Text) |> String.concat " "
                raise (DashSpecParseException($"Unexpected statement '{lineText}' in ports block (expected default, input, param, or output)."))

        let inputsArr = inputs.ToArray()
        let outputsArr = outputs.ToArray()

        defaultInput |> Option.iter (fun name -> validateDefaultPort "input" name inputsArr)
        defaultOutput |> Option.iter (fun name -> validateDefaultPort "output" name outputsArr)

        { Inputs = inputsArr
          Outputs = outputsArr
          Params = paramsDecls.ToArray()
          DefaultInput = defaultInput
          DefaultOutput = defaultOutput }

    /// Compatibility wrapper returning `Some` over `projectPortsFromAst`.
    let tryProjectPortsFromAst (members: DashSpecAstNode[]) : ParsedPorts option =
        try
            Some(projectPortsFromAst members)
        with :? DashSpecParseException -> None
