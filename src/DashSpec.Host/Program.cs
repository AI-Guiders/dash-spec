using DashSpec.Execution.Compilation;
using DashSpec.Execution.Parsing;
using DashSpec.Host.Configuration;
using DashSpec.Host.Services.Abstractions;
using DashSpec.Composition.Plugins;
using DashSpecParser = DashSpec.Execution.Parsing.DashSpecParser;

if (args is ["validate", ..])
{
    // validate moved to dsc (DASHSPEC-ADR-0117 steps 3–4): the CLI is the second
    // composition root with full plugin-vocabulary parity; the web Host no longer
    // carries a builtins-only validator that saw a different language than the runtime.
    Console.Error.WriteLine("validate moved to dsc (DASHSPEC-ADR-0117). Run: dsc validate <path> [--toml <dash-spec.toml>]");
    Environment.ExitCode = 2;
    return;
}

var builder = WebApplication.CreateBuilder(args);

// Production + `dotnet run` does not load staticwebassets.runtime.json by default —
// Blazor _framework/*.js then 404/500 (see aspnetcore#65468). Publish is fine; local prod smoke needs this.
builder.WebHost.UseStaticWebAssets();

_ = await builder.AddDashSpecHostAsync();

var app = builder.Build();
app.UseDashSpecHost();
app.Run();
