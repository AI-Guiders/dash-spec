namespace DashSpec.Modeling.Parse.Document

open System
open System.Collections.Generic
open DashSpec.Modeling.Core

/// <summary>One <c>import &lt;kind&gt; from &lt;namespace&gt; [as alias]</c> line from the module header (ADR-0098).</summary>
type ModuleImportDirective =
    { Kind: ImportKind
      Namespace: string
      Alias: string option }

module ModuleImportDirective =

    /// <summary>Default qualifier when <c>as</c> is omitted — last segment of the namespace.</summary>
    let defaultQualifier (namespaceName: string) =
        let segments = namespaceName.Split('.', StringSplitOptions.RemoveEmptyEntries)
        if segments.Length = 0 then namespaceName else segments.[segments.Length - 1]

    let effectiveQualifier (directive: ModuleImportDirective) =
        match directive.Alias with
        | Some alias -> alias
        | None -> defaultQualifier directive.Namespace

type ModuleHeader =
    { Namespace: string option
      Imports: IReadOnlyList<ModuleImportDirective> }

module ModuleHeader =
    let empty = { Namespace = None; Imports = [||] }
