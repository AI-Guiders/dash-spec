using DashSpec.Execution.Runtime.Session;
using DashSpec.Execution.Session;
using Xunit;

namespace DashSpec.Platform.Session.Tests;

/// <summary>L3 scaffold — headless session types ship with ADR-0099 B2.</summary>
public sealed class ReportSessionContractTests
{
    [Fact]
    public void ReportSession_and_preview_session_are_platform_distinct_types()
    {
        Assert.NotEqual(typeof(ReportSession), typeof(ReportPreviewSession));
        Assert.True(typeof(ReportSession).IsPublic);
    }
}
