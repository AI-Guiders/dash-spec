---
type: ADR
id: DASHSPEC-ADR-0102
title: "One language"
status: "Accepted"
date: unknown
tags: [dashspec, adr]
language: ru
relates:
  - DASHSPEC-ADR-0024
  - DASHSPEC-ADR-0098
  - DASHSPEC-ADR-0101
---

# DASHSPEC-ADR-0102: One language

## Decision

DashSpec has one language. Legacy forms are removed, not kept behind compatibility branches.

- A file is one unit: `module <kind> <id>` followed by block sections.
- `<kind>` is looked up in `ImportKindRegistry`. An unknown kind is an error.
- Inside `card`, only the new forms exist: `data flow { … }` and `datasource sql` (ADR-0018). `data { }`, `datasource` without `sql`, `input`, inline flow are removed.
- Imports: `import <kind> from <Namespace> [as alias]`. Namespaces are found by scanning `namespace` headers under the source roots. No index tables.
- `!include`, `@`-roots, brace blocks, and the `LegacySequential` link mode are removed.
- Unknown keywords produce one generic error with the position. There are no per-construct `rejectLegacy*` guards.

## Consequences

- Sources that use legacy forms stop loading. Migration of specs happens after the parser change, in one pass.
- `samples/demo` and product specs are migrated to the new forms as part of that pass.
- Guard functions (`rejectLegacyCardData`, `rejectLegacyFlow`, `rejectLegacyTooltipProperties`, and similar) are deleted together with the code they guard.
