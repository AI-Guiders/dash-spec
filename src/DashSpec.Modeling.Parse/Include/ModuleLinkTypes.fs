namespace DashSpec.Modeling.Parse.Include

/// Envelope link directive (ADR-0089): path membership, not preprocessor paste order.
type ModuleLinkDirective =
    | PathReference of string
    | DiagramFrom of diagramId: string * path: string

type ModuleLinkMode =
    | LegacySequential = 0
    | MembershipUnion = 1
