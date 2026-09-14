namespace DashSpec.Modeling.Parse.Document

/// <summary>Cross-field dashboard validation — delegates to Execution bridge when registered.</summary>
module DashboardValidator =

    let validate document = DashboardValidationBridge.validate document
