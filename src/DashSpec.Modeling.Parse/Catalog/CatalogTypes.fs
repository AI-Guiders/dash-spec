namespace DashSpec.Modeling.Parse.Catalog

open System.Collections.Generic

[<CLIMutable>]
type CatalogEntryDefinition =
    { Id: string
      Title: string
      DashspecPath: string
      GroupId: string option }

[<CLIMutable>]
type CatalogGroupDefinition = { Id: string; Title: string }

[<CLIMutable>]
type CatalogDocument =
    { Id: string
      DefaultEntryId: string
      Entries: IReadOnlyList<CatalogEntryDefinition>
      Groups: IReadOnlyList<CatalogGroupDefinition> option }