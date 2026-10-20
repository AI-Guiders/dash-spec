namespace DashSpec.Modeling.Parse

open System
open System.Collections.Generic
open DashSpec.Modeling.Parse.Import

module PhraseScopes =
    let onClick = "card.on_click"

type PhraseSlotKind =
    | Ident = 0
    | String = 1
    | Int = 2

[<CLIMutable>]
type PhraseSlotDescriptor =
    { Name: string
      Kind: PhraseSlotKind
      Optional: bool }

[<CLIMutable>]
type PhraseTemplateDescriptor =
    { PluginId: string
      HandlerId: string
      Scope: string
      Pattern: string
      Slots: IReadOnlyList<PhraseSlotDescriptor> }

[<CLIMutable>]
type DashSpecParseOptions =
    { MergeReferencedTabModules: bool
      ExtensionBlockKeywords: IReadOnlySet<string>
      ExtensionBlockPluginIds: IReadOnlyDictionary<string, string>
      PhraseTemplates: IReadOnlyList<PhraseTemplateDescriptor>
      KnownActionHandlers: IReadOnlySet<string>
      KnownInteractionHandlers: IReadOnlySet<string>
      /// Click sources declared by viz plugins (ADR-0107: matrix renders `cell`, `category item`).
      /// Empty set skips source validation (dev/test convenience, same rule as action handlers).
      KnownClickSources: IReadOnlySet<string> }

module DashSpecParseOptions =

    let defaultOptions =
        { MergeReferencedTabModules = true
          ExtensionBlockKeywords = HashSet<string>(StringComparer.OrdinalIgnoreCase) :> IReadOnlySet<_>
          ExtensionBlockPluginIds = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) :> IReadOnlyDictionary<_, _>
          PhraseTemplates = Array.empty
          KnownActionHandlers = HashSet<string>(StringComparer.OrdinalIgnoreCase) :> IReadOnlySet<_>
          KnownInteractionHandlers = HashSet<string>(StringComparer.OrdinalIgnoreCase) :> IReadOnlySet<_>
          KnownClickSources = HashSet<string>(StringComparer.OrdinalIgnoreCase) :> IReadOnlySet<_> }
