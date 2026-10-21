namespace DashSpec.Modeling.Parse.Layout

open System.Collections.Generic
open DashSpec.Modeling.Core

/// <summary>
/// Where a placement grid lives ([ADR-0072]); pure enum, relocated from C# <c>Execution.Runtime</c>
/// to the model layer beside its siblings ([ADR-0111]) — the C# engine keeps producing it.
/// </summary>
type LayoutSlotScope =
    | HostTabBoard = 0
    | HostPageToolbar = 1
    | CardInterior = 2
    | CardFilterChrome = 3

/// <summary>
/// Slot provenance ([ADR-0109] spirit / [ADR-0111]): the assembly records substituted defaults
/// instead of applying them silently.
/// </summary>
type PlacementProvenance =
    | Declared = 0
    | RecordedDefault = 1

/// <summary>
/// Route of a board slice ([ADR-0111]): pages own boards and filter cards
/// (<c>TabLayoutCompactor.ResolveContext</c>), so the effective board is precomputed per route —
/// all tabs × their pages, plus the pageless/tabless document board.
/// </summary>
[<CLIMutable>]
type BoardRoute =
    { TabId: string option
      PageId: string option }

/// <summary>
/// One assembled slot ([ADR-0111]): the occupant is literally a node of the document wiring graph
/// ([ADR-0091]) — placed as is, nothing resolved, no copies; placement/scope are the existing records.
/// </summary>
[<CLIMutable>]
type Slot =
    { Node: WiringNode
      Placement: PlacementDefinition
      Scope: LayoutSlotScope
      Provenance: PlacementProvenance }

/// <summary>Recorded initial route ([ADR-0111]): a default, recorded at assembly ([ADR-0109]).</summary>
[<CLIMutable>]
type VisualRoute =
    { EntryId: string
      TabId: string
      PageId: string }

/// <summary>
/// The assembled slot plane ([ADR-0111]): every visual slice precomputed once per document revision —
/// the render reads it and places. Route-independent; switching a tab picks a slice, never rebuilds.
/// </summary>
[<CLIMutable>]
type SlotPlane =
    { Boards: IReadOnlyDictionary<BoardRoute, IReadOnlyList<Slot>>
      Toolbars: IReadOnlyDictionary<BoardRoute, IReadOnlyList<Slot>>
      CardInteriors: IReadOnlyDictionary<string, IReadOnlyList<Slot>>
      CardFilterChrome: IReadOnlyDictionary<string, IReadOnlyList<Slot>>
      InitialRoute: VisualRoute }
