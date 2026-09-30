namespace DashSpec.Modeling.Parse.Toolbar

open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing

module CommandAliasesParser =

    let parse (reader: TokenReader) =
        MemberGrammar.parseIdentMapBlock reader "commands" "commands"
        :> System.Collections.Generic.IReadOnlyDictionary<_, _>
