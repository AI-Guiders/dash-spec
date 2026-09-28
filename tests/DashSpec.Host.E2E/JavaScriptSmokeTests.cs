using DashSpec.Host.E2E.Hosting;
using DashSpec.Host.E2E.Playwright;
using Microsoft.Playwright;
using Xunit;

namespace DashSpec.Host.E2E;

[Collection(nameof(E2eTestCollection))]
public sealed class JavaScriptSmokeTests
{
    private readonly DashSpecWebApplicationFactory _factory;
    private readonly PlaywrightBrowserFixture _playwright;

    public JavaScriptSmokeTests(
        DashSpecWebApplicationFactory factory,
        PlaywrightBrowserFixture playwright)
    {
        _factory = factory;
        _playwright = playwright;
        _ = factory.CreateClient();
    }

    [Fact]
    public async Task First_party_scripts_return_200_and_parse()
    {
        using var client = _factory.CreateClient();
        foreach (var path in HostScriptUrls.FirstPartyScripts)
        {
            var response = await client.GetAsync(path);
            Assert.True(
                response.IsSuccessStatusCode,
                $"GET {path} failed: {(int)response.StatusCode} {response.ReasonPhrase}");
        }

        var page = await _playwright.Browser.NewPageAsync();
        var capture = new BrowserConsoleCapture();
        capture.Attach(page);

        var baseUrl = _factory.BaseUrl;
        await page.GotoAsync($"{baseUrl}/help", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = 120_000,
        });

        await page.WaitForSelectorAsync(".help-page h1", new PageWaitForSelectorOptions { Timeout = 60_000 });

        await page.EvaluateAsync(@"() => {
            if (typeof window.dashSpecMatrix !== 'object') {
                throw new Error('dashSpecMatrix is not loaded');
            }
            if (typeof window.dashSpecCharts !== 'object') {
                throw new Error('dashSpecCharts is not loaded');
            }
            if (typeof window.Chart !== 'function') {
                throw new Error('Chart.js is not loaded');
            }
        }");

        capture.AssertClean();
    }

    [Fact]
    public async Task Help_page_has_no_javascript_errors()
    {
        var page = await _playwright.Browser.NewPageAsync();
        var capture = new BrowserConsoleCapture();
        capture.Attach(page);

        var baseUrl = _factory.BaseUrl;
        await page.GotoAsync($"{baseUrl}/help", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = 120_000,
        });

        await page.WaitForSelectorAsync(".help-page", new PageWaitForSelectorOptions { Timeout = 60_000 });
        await page.WaitForTimeoutAsync(1500);

        capture.AssertClean();
    }

    [Fact]
    public async Task Home_dashboard_has_no_javascript_errors()
    {
        var page = await _playwright.Browser.NewPageAsync();
        var capture = new BrowserConsoleCapture();
        capture.Attach(page);

        var baseUrl = _factory.BaseUrl;
        await page.GotoAsync(baseUrl + "/", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = 180_000,
        });

        await page.WaitForSelectorAsync(
            ".card, .card-skeleton, .card-error, .dashboard-shell",
            new PageWaitForSelectorOptions { Timeout = 180_000 });

        await page.WaitForTimeoutAsync(3000);

        capture.AssertClean();

        var matrixPainted = await page.EvaluateAsync<bool>(@"() => {
            const canvas = document.querySelector('canvas.matrix-canvas');
            return canvas != null && canvas.width > 1 && canvas.height > 1;
        }");

        if (await page.EvaluateAsync<bool>("() => document.querySelector('canvas.matrix-canvas') != null"))
        {
            Assert.True(matrixPainted, "matrix canvas element present but bitmap is still 1×1 (JS paint did not run)");
        }
    }
}
