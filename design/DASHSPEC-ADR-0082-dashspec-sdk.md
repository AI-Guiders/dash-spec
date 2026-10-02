# DASHSPEC-ADR-0082: DashSpec SDK (plugin author surface)

| | |
|---|---|
| **Status** | Accepted (packaging phased) |
| **Date** | 2026-10-02 |
| **Relates to** | [ADR-0001](DASHSPEC-ADR-0001-connectors-as-plugins.md), [ADR-0080](DASHSPEC-ADR-0080-dataflow-transform-plugins.md), [ADR-0081](DASHSPEC-ADR-0081-type-plugins.md) |

## Context

Connectors already depend on **`DashSpec.Abstractions`** in-repo (`connectors/DashSpec.Connector.*`). Transforms and semantic types ([ADR-0080](DASHSPEC-ADR-0080-dataflow-transform-plugins.md), [ADR-0081](DASHSPEC-ADR-0081-type-plugins.md)) add more plugin surfaces. Report authors stay on stdlib (~95%); **platform devs** need a **stable, versioned contract** outside the monorepo — not project references to `src/DashSpec.Core`.

Without an explicit SDK boundary, plugin authors accidentally reference Host, Modeling, or Execution internals and break on every refactor.

## Decision

### Product split

| Ship to plugin authors | Internal (not in SDK) |
|------------------------|------------------------|
| Plugin interfaces + DTOs (queries, manifests, registries) | Parser, `.dashspec` AST, F# Modeling |
| Documented manifest TOML (`[[plugins.load]]`) | Host Blazor UI, layout, filters UI |
| Semver + changelog for breaking interface changes | Execution graph runner, SQL compiler |
| Templates + sample projects | Builtin implementations (may **reference** SDK only) |

**DashSpec SDK** = **NuGet-delivered contracts** + **docs/templates**, not a fork of the platform.

### NuGet packages (v1 layout)

| Package | Contents | Consumer |
|---------|----------|----------|
| **`DashSpec.Abstractions`** | `IConnectorPlugin`, `IDataSourceConnector`, `CompiledQuery`, …; **add** `ITransformPlugin`, `IDataFlowTransform`, `ITypePlugin`, `IValueType`, shared manifest DTOs | All plugin dlls |
| **`DashSpec.Sdk`** (meta) | `PackageReference` to `DashSpec.Abstractions` + README link; optional future `DashSpec.Sdk.Analyzers` | `dotnet new` projects |
| **`DashSpec.Sdk.Templates`** (later) | `dashspec-connector`, `dashspec-transform`, `dashspec-type`, `dashspec-host-extension` | Platform dev CLI |

Host and builtins **implement** SDK types; they do not re-export Core/Modeling types to plugins.

Repo mapping today:

```text
src/DashSpec.Abstractions/     → pack as DashSpec.Abstractions
connectors/DashSpec.Connector.*  → reference SDK package (or ProjectReference in monorepo only)
transforms/ (future)             → same pattern as connectors/
```

### Versioning

- **SDK semver** tracks **`DashSpec.Abstractions`** assembly version; aligned with Host **major** on breaking plugin API.
- Plugin dll declares **supported SDK range** in manifest (optional v1.1): `sdk_min = "0.2"`, `sdk_max = "0.x"`.
- Report **spec** version (`@dashspec` language) is independent of SDK — authors on stdlib never pin SDK.

### Documentation surface

Single entry: **`docs/sdk/README.md`** (RU + EN later):

- which interface for which plugin kind;
- manifest + folder layout (`connectors/`, `transforms/`, `plugins/`);
- local F5: copy dll next to Host or `[[plugins.load]]`;
- **do not reference** `DashSpec.Core`, `DashSpec.Modeling.*`, `DashSpec.Execution.*` from product plugins.

### Future package split (non-blocking)

`DashSpec.Abstractions` today references **`Microsoft.AspNetCore.App`** for Host extension plugins (`IDashSpecPlugin`, viz). When transform/type plugins land, consider:

| Package | Purpose |
|---------|---------|
| `DashSpec.Sdk.DataFlow` | Transforms + types + row DTOs — **no** ASP.NET |
| `DashSpec.Sdk.Hosting` | Card/viz/endpoint contributors |

Meta `DashSpec.Sdk` references both. Monorepo can merge until download size hurts.

## Phased delivery

| Phase | Deliverable |
|-------|-------------|
| **S0** | This ADR; `IsPackable` + `PackageId` on `DashSpec.Abstractions`; `docs/sdk/README.md` |
| **S1** | CI `dotnet pack` artifact; publish to GitHub Packages / internal feed; connectors sample uses PackageReference in optional `samples/sdk-connector/` |
| **S2** | Transform/type interfaces in Abstractions; `dotnet new` templates |
| **S3** | `DashSpec.Sdk.Testing` — in-memory harness to run one transform against fixture rows (no full Host) |

## Non-goals

- Shipping Modeling F# or parser as NuGet for “custom language extensions”.
- SDK that allows arbitrary spec eval or scripting host APIs without tier review ([`PluginTier`](../../src/DashSpec.Abstractions/Plugins/PluginTier.cs) stays for host extensions).

## Consequences

- **Power author** consumes **vendor NuGet** + manifest ids; **platform dev** uses **SDK + templates**.
- Clear review boundary: public API = Abstractions XML docs + ADR; everything else is implementation detail.
- demo / demo product repos can live outside `dash-spec` with pinned `DashSpec.Abstractions` — same as connector split in product ADRs.
