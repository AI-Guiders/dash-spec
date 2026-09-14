namespace DashSpec.Modeling.Parse.Document

open System

/// <summary>
/// Execution registers cross-field validation (Core Analysis) into F# parse path (ADR-0048 M7).
/// </summary>
module DashboardValidationBridge =

    let mutable private hook: DashboardDocument -> unit = ignore

    let register (validate: DashboardDocument -> unit) = hook <- validate

    let registerAction (validate: Action<DashboardDocument>) =
        register (fun document -> validate.Invoke document)

    let validate document = hook document
