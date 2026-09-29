# DASHSPEC-ADR-0067: Card fold chrome (BI viewport)

| | |
|---|---|
| **Status** | Accepted (v1: card body fold) |
| **Date** | 2026-09-29 |
| **Audience** | Authors: `chrome` / `cards chrome` in `.dashspec`; Host engineers: session fold state |
| **Relates to** | [ADR-0065](DASHSPEC-ADR-0065-card-interior-multi-slot.md), [ADR-0035](DASHSPEC-ADR-0035-chrome-and-filter-widget-families.md), [ADR-0028](DASHSPEC-ADR-0028-bounded-card-click-interactions.md) |

## Context

Dense BI pages stack heavy cards (heatmap + drill). Authors need to **free vertical space** without tabs/phases — collapse card body to titlebar or **focus one card** (expand lower, collapse upper).

## Decision (author SSOT)

### Report policy (optional v1)

```text
report
  cards chrome
    fold = focus_single | focus | none
  end chrome
end report
```

| `fold` | Semantics |
|--------|-----------|
| `none` (default) | Report does not coordinate cards; each `independent` card toggles on its own |
| `focus_single` | Expanding a foldable card collapses other foldable cards on the page |

Per-card `fold = independent` means **this card** has its own chevron and body toggle. It does **not** imply `focus_single`; add `cards chrome { fold = focus_single }` on the report when you want one expanded card at a time.

### Per card

```text
card peak as "…"
  chrome
    fold = independent | none
  end chrome
end card
```

| `fold` | Semantics |
|--------|-----------|
| `none` | No fold control (default) |
| `independent` | Titlebar chevron; toggle **this** card body (interior grid). No SQL re-run |

Fold hides **card body** (interior grid + viz), keeps **card-head** (title, local filters, actions).

### Slot fold (interior)

**Non-goal v1.** Hiding only `drill` row while keeping heatmap uses future `chrome` on slot or `layout` hint — not separate card fold.

### Runtime

- State: **session** (`ICardFoldState`), cleared on spec reload.
- Collapse/expand: **presentation only** — no `ApplyFilters`, no `Card.Loading`.

## Examples (demo overview)

```text
cards chrome
  fold = focus_single
end cards chrome

card peak_concurrent_proxy …
  chrome
    fold = independent
  end chrome
```

## Consequences

- Parser: `CardChromeParser.fold`, `CardsChromeParser`
- Core: `CardFoldMode`, `CardsChromeDefinition`
- Host: chevron, `.card--folded`, `ToggleCardFold` + focus policy
