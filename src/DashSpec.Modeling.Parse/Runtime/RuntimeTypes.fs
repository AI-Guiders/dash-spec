namespace DashSpec.Modeling.Parse.Runtime

open System
open System.Collections.Generic

[<CLIMutable>]
type RuntimeProviderDefinition =
    { Id: string
      Connector: string
      Server: string option
      Port: int option
      Database: string option
      User: string option
      Password: string option
      IntegratedSecurity: bool option
      Encrypt: bool option
      TrustServerCertificate: bool option
      TimeoutSeconds: int
      MaxRows: int
      ConnectionString: string }

[<CLIMutable>]
type RuntimeDocument =
    { Id: string
      Namespace: string option
      DefaultProviderId: string
      ActiveBundle: string
      Providers: IReadOnlyList<RuntimeProviderDefinition>
      Plugins: IReadOnlyList<string> }
