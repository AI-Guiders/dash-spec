namespace DashSpec.Modeling.Parse.Syntax

/// Stable AST node identity (planet parse tree). Federation `NodeId` is minted at bridge.
[<Struct; StructuralEquality; StructuralComparison>]
type AstNodeId = AstNodeId of value: uint32

module AstNodeId =
    let zero = AstNodeId 0u

    let create (value: uint32) = AstNodeId value

    let value (AstNodeId v) = v

    let next (AstNodeId v) = AstNodeId(v + 1u)
