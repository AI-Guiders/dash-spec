using Microsoft.Playwright;
using Xunit;

namespace DashSpec.Host.E2E.Playwright;

public sealed class BrowserConsoleCapture
{
    private readonly List<string> _consoleErrors = [];
    private readonly List<string> _pageErrors = [];
    private readonly List<string> _failedScripts = [];

    public IReadOnlyList<string> ConsoleErrors => _consoleErrors;

    public IReadOnlyList<string> PageErrors => _pageErrors;

    public IReadOnlyList<string> FailedScripts => _failedScripts;

    public void Attach(IPage page)
    {
        page.Console += (_, msg) =>
        {
            if (msg.Type == "error")
            {
                var location = msg.Location;
                if (!string.IsNullOrEmpty(location) && IsDevToolingProbe(location))
                {
                    return;
                }

                _consoleErrors.Add(string.IsNullOrEmpty(location) || location == msg.Text ? msg.Text : $"{msg.Text} (at {location})");
            }
        };

        page.PageError += (_, error) => _pageErrors.Add(error);

        page.Response += (_, response) =>
        {
            if (response.Status >= 400 && !IsDevToolingProbe(response.Url))
            {
                _consoleErrors.Add($"http {response.Status}: {response.Url}");
            }
        };

        page.RequestFailed += (_, request) =>
        {
            if (IsScriptRequest(request.Url))
            {
                _failedScripts.Add($"{request.Url} — {request.Failure}");
            }
        };
    }

    public void AssertClean()
    {
        var issues = new List<string>();
        issues.AddRange(_pageErrors.Select(x => $"pageerror: {x}"));
        issues.AddRange(_consoleErrors.Select(x => $"console.error: {x}"));
        issues.AddRange(_failedScripts.Select(x => $"script.load: {x}"));

        Assert.True(issues.Count == 0, "JavaScript issues detected:\n" + string.Join("\n", issues));
    }

    private static bool IsScriptRequest(string url) =>
        url.Contains("/js/", StringComparison.OrdinalIgnoreCase) ||
        url.Contains(".js?", StringComparison.OrdinalIgnoreCase) ||
        url.EndsWith(".js", StringComparison.OrdinalIgnoreCase);

    /// <summary>The Blazor WASM dev hot-reload module (Debug SDK injection) probes for
    /// hot-reload settings that exist only inside a live dev session — its 404 is tooling
    /// noise, not an application resource failing.</summary>
    private static bool IsDevToolingProbe(string url) =>
        url.Contains("/_framework/dotnet-browser-tools/", StringComparison.OrdinalIgnoreCase);
}
