namespace DashSpec.Host.E2E.Playwright;

/// <summary>Must stay in sync with <c>Components/App.razor</c> script tags.</summary>
public static class HostScriptUrls
{
    public static readonly string[] FirstPartyScripts =
    [
        "/lib/chartjs/chart.umd.min.js",
        "/lib/chartjs/chartjs-chart-boxplot.umd.min.js",
        "/_content/DashSpec.Plugin.Viz.Builtins/js/charts.js",
        "/_content/DashSpec.Plugin.Viz.Builtins/js/matrix-canvas.js",
        "/js/aiguiders-input.js",
        "/js/dashspec-download.js",
    ];
}
