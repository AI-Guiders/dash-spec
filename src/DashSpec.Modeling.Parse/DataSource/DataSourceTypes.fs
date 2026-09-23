namespace DashSpec.Modeling.Parse.DataSource

type DataSourceKind =
    | View
    | Sql
    | Xlsx

type DataSourceSqlCarrier =
    | Query
    | File

[<CLIMutable>]
type DataSourceDefinition =
    { Kind: DataSourceKind
      Value: string
      SqlCarrier: DataSourceSqlCarrier option
      Sheet: string option }
