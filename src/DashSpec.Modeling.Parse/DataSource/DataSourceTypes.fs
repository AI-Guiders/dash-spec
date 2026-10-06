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
      Sheet: string option
      RowsType: string
      /// <c>true</c> when <c>datasource { infer … }</c> — manifest <c>default_provider_id</c>.
      ProviderInfer: bool
      ProviderId: string option }
