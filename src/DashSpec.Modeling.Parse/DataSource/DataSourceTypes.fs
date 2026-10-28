namespace DashSpec.Modeling.Parse.DataSource

[<RequireQualifiedAccess>]
type DataSourceKind =
    | View = 0
    | Sql = 1
    | Xlsx = 2

[<RequireQualifiedAccess>]
type DataSourceSqlCarrier =
    | Query = 0
    | File = 1

[<CLIMutable>]
type DataSourceDefinition =
    { Kind: DataSourceKind
      Value: string
      SqlCarrier: DataSourceSqlCarrier option
      Sheet: string option
      RowsType: string
      /// <summary>Хранение времени в источнике: "utc" | "local". Пусто — не заявлено (по умолчанию UTC).</summary>
      TimeZone: string
      /// <c>true</c> when <c>datasource { infer … }</c> — manifest <c>default_provider_id</c>.
      ProviderInfer: bool
      ProviderId: string option }

    member this.WithRowsType(rowsType: string) =
        { this with RowsType = rowsType }
