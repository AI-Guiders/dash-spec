# DASHSPEC-ADR-0034: Phrase templates and document scopes

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-07-05 |
| **Extends** | [ADR-0032](DASHSPEC-ADR-0032-extension-blocks-and-plugins.md), [ADR-0033](DASHSPEC-ADR-0033-plugin-families-and-microkernel-host.md), [ADR-0028](DASHSPEC-ADR-0028-bounded-card-click-interactions.md) |

## Context

Extension plugins register blocks and handlers, but line-level UX (`run`, custom phrases) needs a **SpecFlow-style** surface: plugin declares a **pattern**, Core matches phrase lines inside **fixed scopes** — without a plugin-owned lexer.

[ADR-0033](DASHSPEC-ADR-0033-plugin-families-and-microkernel-host.md) rejects full node ownership (`ReportScopePlugin` replaces Core `report`). Document **skeleton stays Core**; plugins extend **contents** of known containers.

## Decision

### 1. Document scopes (Core skeleton)

| Scope id | Container | Phrase / effect lines |
|----------|-----------|------------------------|
| `card.on_click` | `on click { … }` | `show`, `set`, `goto`, `invoke`, `run`, plugin phrases |
| `card.extension` | extension blocks on card | generic `{ key = value }` ([ADR-0032](DASHSPEC-ADR-0032-extension-blocks-and-plugins.md)) |
| `report` | `report { … }` | filters, cards, layout (Core); optional future containers via scope contributor |

New top-level containers (e.g. `report { actions { … } }`) — **scope contributor** plugin + ADR; rare.

### 2. Phrase templates (plugin)

Plugin registers **pattern + slots + handler** for a scope:

```csharp
registry.AddPhraseTemplate(new PhraseTemplateDescriptor(
    "card_export",
    "csv_export",
    PhraseScopes.OnClick,
    "export card as {format} with delimiter {delimiter}",
    [new("format", PhraseSlotKind.Ident), new("delimiter", PhraseSlotKind.String)]));
```

Core **PhraseTemplateMatcher** matches a phrase line (no custom tokenizer). Slots: `{name}`, `{name}?` (optional). **No expressions.**

### 3. Core invoke / run (first-class)

Inside `on click { }`:

```text
invoke drill_down(from = y)
run csv_export(format = csv, delimiter = ";")
export card as csv with delimiter ";"    # phrase template
```

Parse → `InvokeHandlerEffect(handlerId, args)`.

### 4. Scope-* family (metadata v1)

`ScopeContributorDescriptor` documents allowed children and exports to `/dev/capabilities`. **Parse hooks for new containers** — follow-up; v1 built-in scopes only.

### 5. Rejected

| Idea | Why |
|------|-----|
| Plugin-owned lexer | Greenspun / inconsistent specs |
| Full `CardScopePlugin` owning `card { }` | breaks predictable skeleton |
| Arbitrary regex `(.*)` per plugin | zoo; typed slots only |
| Phrase lines anywhere in file | scope whitelist only |

## Consequences

- **Abstractions:** `PhraseScopes`, `PhraseTemplateDescriptor`, `ScopeContributorDescriptor`, registry APIs.
- **Core:** `PhraseTemplateMatcher`, `InvokeHandlerEffect`, `CardClickParser` dispatch, `(` `)` in lexer for call args.
- **Host:** register scope metadata + phrase templates from capability plugins.
- **Lint (follow-up):** unknown phrase, ambiguous template overlap, handler not in `extensions { use }`.
