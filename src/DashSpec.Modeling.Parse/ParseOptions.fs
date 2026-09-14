namespace DashSpec.Modeling.Parse

open System
open System.Collections.Generic

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
      TolerateIncompleteIncludes: bool
      ExtensionBlockKeywords: IReadOnlySet<string>
      ExtensionBlockPluginIds: IReadOnlyDictionary<string, string>
      PhraseTemplates: IReadOnlyList<PhraseTemplateDescriptor>
      KnownActionHandlers: IReadOnlySet<string>
      KnownInteractionHandlers: IReadOnlySet<string> }

module DashSpecParseOptions =

    let defaultOptions =
        { MergeReferencedTabModules = true
          TolerateIncompleteIncludes = false
          ExtensionBlockKeywords = HashSet<string>(StringComparer.OrdinalIgnoreCase) :> IReadOnlySet<_>
          ExtensionBlockPluginIds = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) :> IReadOnlyDictionary<_, _>
          PhraseTemplates = Array.empty
          KnownActionHandlers = HashSet<string>(StringComparer.OrdinalIgnoreCase) :> IReadOnlySet<_>
          KnownInteractionHandlers = HashSet<string>(StringComparer.OrdinalIgnoreCase) :> IReadOnlySet<_> }

    let editor =
        { defaultOptions with
            MergeReferencedTabModules = false
            TolerateIncompleteIncludes = true
            ExtensionBlockKeywords = HashSet<string>([ "views" ], StringComparer.OrdinalIgnoreCase) :> IReadOnlySet<_> }
