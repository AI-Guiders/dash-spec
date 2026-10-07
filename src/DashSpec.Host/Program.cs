using DashSpec.Core.Parsing;
using DashSpec.Core.Validation;
using DashSpec.Host.Configuration;
using DashSpec.Host.Services.Abstractions;
using DashSpec.Viewer.Plugins;
using DashSpecParser = DashSpec.Execution.Parsing.DashSpecParser;

if (args is ["validate", var validatePath, ..])
{
    try
    {
        var fullPath = Path.GetFullPath(validatePath);
        if (fullPath.EndsWith(".dashcatalog", StringComparison.OrdinalIgnoreCase))
        {
            DashSpecValidator.ValidateCatalog(fullPath);
        }
        else if (fullPath.EndsWith(".dashhost", StringComparison.OrdinalIgnoreCase))
        {
            DashSpecParser.EnsureModuleParsersRegistered();
            HostModuleParser.ParseFile(fullPath);
        }
        else
        {
            DashSpecParser.EnsureModuleParsersRegistered();
            var registry = DashSpecBuiltinContributorRegistrar.RegisterBuiltins();
            var parseOptions = new DashSpecParseOptionsProvider(registry).CreateOptions();
            var specDirectory = Path.GetDirectoryName(fullPath)!;
            DashSpecValidator.ValidateSpec(fullPath, specDirectory, parseOptions);
        }

        Console.WriteLine($"OK: {fullPath}");
        return;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(ex.Message);
        Environment.ExitCode = 1;
        return;
    }
}

var builder = WebApplication.CreateBuilder(args);

// Production + `dotnet run` does not load staticwebassets.runtime.json by default —
// Blazor _framework/*.js then 404/500 (see aspnetcore#65468). Publish is fine; local prod smoke needs this.
builder.WebHost.UseStaticWebAssets();

_ = await builder.AddDashSpecPlanetHostAsync();

var app = builder.Build();
app.UseDashSpecPlanetHost();
app.Run();
