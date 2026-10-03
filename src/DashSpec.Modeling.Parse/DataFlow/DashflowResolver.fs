namespace DashSpec.Modeling.Parse.DataFlow

open System
open System.IO
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.Include
/// <summary>Resolve <c>.dashflow</c> paths and parse with a <see cref="TypeCatalog"/>.</summary>
module DashflowResolver =

    let resolveFlowFilePath (reference: string) (specDirectory: string) =
        if String.IsNullOrWhiteSpace reference then
            invalidArg "reference" "Flow path is required."

        let path = SpecFragmentPaths.resolvePath reference specDirectory

        if File.Exists path then
            path
        elif File.Exists(path + ".dashflow") then
            path + ".dashflow"
        else
            raise (FileNotFoundException($"Dashflow not found: '{reference}' (resolved: '{path}').", path))

    let parseFile (path: string) (catalog: TypeCatalog) =
        DashflowModuleParser.parseModule (File.ReadAllText path) catalog

    let parseReference (reference: string) (specDirectory: string) (catalog: TypeCatalog) =
        let path = resolveFlowFilePath reference specDirectory
        path, parseFile path catalog
