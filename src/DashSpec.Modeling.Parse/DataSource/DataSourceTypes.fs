namespace DashSpec.Modeling.Parse.DataSource

type DataSourceKind =
    | View
    | Sql

type DataSourceSqlCarrier =
    | Query
    | File

[<CLIMutable>]
type DataSourceDefinition =
    { Kind: DataSourceKind
      Value: string
      SqlCarrier: DataSourceSqlCarrier option }
