namespace DashSpec.Modeling.Parse.Tests

open System.IO
open System.Runtime.CompilerServices

/// <summary>Parser tests are pure Modeling.Parse (no Execution.Core): its embedded-stdlib
/// module initializer never runs here, so this project registers the repo stdlib folder
/// the same way — mirroring what Execution.Core does from its assembly manifest.</summary>
module StdlibFromRepoRegistration =

    let private repoStdlib =
        Path.GetFullPath(
            Path.Combine(__SOURCE_DIRECTORY__, "..", "..", "src", "DashSpec.Execution.Core", "stdlib"))

    let register () =
        if Directory.Exists repoStdlib then
            Directory.EnumerateFiles(repoStdlib, "*.ds", SearchOption.AllDirectories)
            |> Seq.map (fun path ->
                let virtualPath =
                    "stdlib/" + Path.GetRelativePath(repoStdlib, path).Replace('\\', '/')
                (virtualPath, File.ReadAllText path))
            |> DashSpec.Modeling.Parse.Document.UnitSource.registerEmbeddedUnits
            |> ignore
