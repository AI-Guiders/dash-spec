namespace DashSpec.Modeling.Parse.Host

open System.Collections.Generic

[<CLIMutable>]
type HostLinkDefinition =
    { Id: string
      Label: string
      Url: string
      Target: string
      Topbar: bool
      Settings: bool }

[<CLIMutable>]
type HostDocument =
    { Id: string
      CatalogPath: string
      Configuration: IReadOnlyDictionary<string, string>
      Presentation: IReadOnlyDictionary<string, string>
      Links: IReadOnlyList<HostLinkDefinition>
      Surfaces: IReadOnlyList<string>
      TopbarLayout: DashSpec.Modeling.Parse.Layout.LayoutBoardDefinition option }
