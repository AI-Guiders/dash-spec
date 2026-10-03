namespace DashSpec.Modeling.Parse.DataFlow

module DashflowProviderBridge =

    let isInfer (binding: DashflowProviderBinding) =
        match binding with
        | DashflowProviderBinding.Infer -> true
        | DashflowProviderBinding.Named _ -> false

    let tryNamedId (binding: DashflowProviderBinding) =
        match binding with
        | DashflowProviderBinding.Named id -> Some id
        | DashflowProviderBinding.Infer -> None
