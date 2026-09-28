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
                _consoleErrors.Add(msg.Text);
            }
        };

        page.PageError += (_, error) => _pageErrors.Add(error);

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
}
