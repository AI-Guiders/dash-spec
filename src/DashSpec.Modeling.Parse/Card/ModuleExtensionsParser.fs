namespace DashSpec.Modeling.Parse.Card

open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing

module ModuleExtensionsParser =

    let parse (reader: TokenReader) =
        BlockSyntax.beginBlock reader
        reader.SkipNewlines()
        let enabled = ResizeArray<string>()
        let imports = ResizeArray<ModuleExtensionImport>()

        while not (BlockSyntax.isBlockEnd reader "extensions" None) && not reader.IsEof do
            reader.SkipNewlines()
            if BlockSyntax.isBlockEnd reader "extensions" None then ()
            elif reader.TryKeyword "use" then
                enabled.Add(reader.ReadIdent())
                reader.SkipNewlines()
            elif reader.TryKeyword "extension" || reader.TryKeyword "import" then
                let pluginId = reader.ReadIdent()
                if reader.TryKeyword "import" || reader.TryKeyword "from" then
                    if not (reader.TryKeyword "from") then reader.ExpectKeyword "from"
                    let path = reader.ReadString()
                    imports.Add({ PluginId = pluginId; AssemblyPath = Some path })
                else
                    enabled.Add(pluginId)
                reader.SkipNewlines()
            else
                raise (reader.Unexpected "use, import, or extension")

        BlockSyntax.expectBlockEnd reader "extensions" None
        { EnabledPluginIds = enabled :> IReadOnlyList<_>; Imports = imports :> IReadOnlyList<_> }
