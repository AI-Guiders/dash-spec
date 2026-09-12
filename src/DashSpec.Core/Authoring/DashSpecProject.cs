using AIGuiders.Platform.Authoring.Core;

namespace DashSpec.Core.Authoring;

/// <summary>Transitional shim — implementation lives in <see cref="Execution.Authoring.DashSpecProject"/>.</summary>
public static class DashSpecProject
{
    public static DashSpecProjectResult Open(string workspaceRoot, string dashspecPath)
    {
        var result = Execution.Authoring.DashSpecProject.Open(workspaceRoot, dashspecPath);
        return new DashSpecProjectResult
        {
            Project = result.Project,
            Diagnostics = result.Diagnostics,
        };
    }
}

public sealed class DashSpecProjectResult
{
    public AuthoringProject? Project { get; init; }

    public IReadOnlyList<AuthoringDiagnostic> Diagnostics { get; init; } = [];
}
