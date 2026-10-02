# DashSpec SDK (plugin authors)

Normative boundary: [DASHSPEC-ADR-0082](../../design/DASHSPEC-ADR-0082-dashspec-sdk.md).

## What you build

| Plugin kind | Interface (in `DashSpec.Abstractions`) | Deploy folder |
|-------------|----------------------------------------|---------------|
| Data source | `IConnectorPlugin`, `IDataSourceConnector` | `connectors/` |
| Dataflow transform | `ITransformPlugin`, `IDataFlowTransform` (ADR-0080) | `transforms/` |
| Semantic type | `ITypePlugin`, `IValueType` (ADR-0081) | `transforms/` or `plugins/` |
| Host extension (card/viz) | `IDashSpecPlugin`, … | `plugins/` |

## What you must not reference

`DashSpec.Core`, `DashSpec.Modeling.*`, `DashSpec.Execution.*`, `DashSpec.Host` — internal; will break without semver notice.

## Package (phased)

```xml
<PackageReference Include="DashSpec.Abstractions" Version="0.2.*" />
```

Meta package `DashSpec.Sdk` will aggregate Abstractions + templates when S1/S2 in ADR-0082 ship.

## Manifest

Same runtime TOML as dashboards — `[[plugins.load]]` with `id` + `path` ([ADR-0001](../../design/DASHSPEC-ADR-0001-connectors-as-plugins.md)).

## Monorepo samples

- `connectors/DashSpec.Connector.SqlServer`
- `connectors/DashSpec.Connector.Postgres`

Copy output dll beside Host or point `path` in manifest.

## Stdlib first

Most dashboards use builtin connector/transform/type ids only. Custom dll is for the ~5% case ([ADR-0081 personas](../../design/DASHSPEC-ADR-0081-type-plugins.md)).
