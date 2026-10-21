namespace DashSpec.Modeling.Parse.Layout

/// <summary>
/// Host default grid bridge ([ADR-0111] §2): the host registers its embedded <c>host.ds</c>
/// grid drawing as the default for documents that draw no grid of their own. No registration
/// and no drawn grid — loud (<see cref="LayoutParser.defaultGrid"/>); there is no silent code default.
/// </summary>
module GridDefaultBridge =

    let mutable Default: LayoutDefinition option = None
