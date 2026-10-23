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
      /// <summary>Settings home block (`host … end host`, ADR-0112 §6) — bootstrap folds it over the legacy blocks.</summary>
      Host: IReadOnlyDictionary<string, string>
      Links: IReadOnlyList<HostLinkDefinition>
      Surfaces: IReadOnlyList<string>
      /// <summary>Shell grid board (scope host): chrome slots + the content slot — rendered by the host itself ([ADR-0111] §2).</summary>
      ShellLayout: DashSpec.Modeling.Parse.Layout.LayoutBoardDefinition option }
