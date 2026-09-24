using DashSpec.Execution.Session;
using Xunit;

namespace DashSpec.Execution.Core.Tests;

public sealed class ReportPreviewSessionTests
{
    static string DemoSoakPath => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..", "..",
        "samples", "demo",
        "demo-soak.dashspec"));

    [Fact]
    public async Task LoadSpecAsync_parses_demo_soak_spec()
    {
        var session = new ReportPreviewSession();

        await session.LoadSpecAsync(DemoSoakPath);

        Assert.Equal("demo_soak", session.Document.Id);
        Assert.Equal(18, session.Document.Cards.Count);
        Assert.Equal(3, session.Document.Tabs.Count);
        Assert.Equal(18, session.Resolved.Cards.Count);
        Assert.NotEmpty(session.Filters.Dates);
    }

    [Fact]
    public async Task GetPayloadFingerprintAsync_is_stable_for_same_spec()
    {
        var first = new ReportPreviewSession();
        var second = new ReportPreviewSession();

        await first.LoadSpecAsync(DemoSoakPath);
        await second.LoadSpecAsync(DemoSoakPath);

        var fingerprintA = await first.GetPayloadFingerprintAsync();
        var fingerprintB = await second.GetPayloadFingerprintAsync();

        Assert.Equal(fingerprintA, fingerprintB);
        Assert.Matches("^[0-9a-f]{64}$", fingerprintA);
    }

    [Fact]
    public async Task RefreshAsync_rebinds_filters_without_changing_fingerprint()
    {
        var session = new ReportPreviewSession();
        await session.LoadSpecAsync(DemoSoakPath);

        var before = await session.GetPayloadFingerprintAsync();
        await session.RefreshAsync();
        var after = await session.GetPayloadFingerprintAsync();

        Assert.Equal(before, after);
    }

    [Fact]
    public async Task GetPayloadFingerprintAsync_changes_when_card_count_differs()
    {
        var workspace = Path.Combine(Path.GetTempPath(), "dashspec-preview-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);
        try
        {
            var oneCardPath = Path.Combine(workspace, "one-card.dashspec");
            var twoCardPath = Path.Combine(workspace, "two-card.dashspec");

            await File.WriteAllTextAsync(oneCardPath, MinimalDashboardSpec(cardCount: 1));
            await File.WriteAllTextAsync(twoCardPath, MinimalDashboardSpec(cardCount: 2));

            var oneCardSession = new ReportPreviewSession();
            var twoCardSession = new ReportPreviewSession();

            await oneCardSession.LoadSpecAsync(oneCardPath);
            await twoCardSession.LoadSpecAsync(twoCardPath);

            var oneCardFingerprint = await oneCardSession.GetPayloadFingerprintAsync();
            var twoCardFingerprint = await twoCardSession.GetPayloadFingerprintAsync();

            Assert.NotEqual(oneCardFingerprint, twoCardFingerprint);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    static string MinimalDashboardSpec(int cardCount)
    {
        var cards = string.Join(
            Environment.NewLine,
            Enumerable.Range(1, cardCount).Select(index => $"""
              card c{index} as "C{index}"
              diagram number
              value = x
              end number
              datasource view dbo.t
              end card
            """));

        return $"""
            @dashboard preview_tab
              runtime
              manifest = "cfg.toml"
              end runtime
              report
              title = "Preview"
            {cards}
              end report
            end dashboard
            """;
    }
}
