#r "D:/Experiments/PersonalCursorFolder/open/dash-spec/src/DashSpec.Execution.Core/bin/Debug/net10.0/DashSpec.Execution.Core.dll"
#r "D:/Experiments/PersonalCursorFolder/open/dash-spec/src/DashSpec.Execution.Runtime/bin/Debug/net10.0/DashSpec.Execution.Runtime.dll"
#r "D:/Experiments/PersonalCursorFolder/open/dash-spec/src/DashSpec.Modeling.Parse/bin/Debug/net10.0/DashSpec.Modeling.Parse.dll"
#r "D:/Experiments/PersonalCursorFolder/open/dash-spec/src/DashSpec.Composition/bin/Debug/net10.0/DashSpec.Composition.dll"

using DashSpec.Execution.Parsing;
using DashSpec.Modeling.Parse.Document;

DashSpecParser.EnsureModuleParsersRegistered();
DashSpec.Composition.Shell.DashSpecDefaultShell.RegisterGridDefault();

// F# module class vs record share the name — fetch defaultOptions via reflection
var asm = typeof(DashSpec.Modeling.Parse.DashSpecParseOptions).Assembly;
var moduleClass = asm.GetTypes().First(t => t.Name == "DashSpecParseOptions" && t.IsAbstract && t.IsSealed);
var options = (DashSpec.Modeling.Parse.DashSpecParseOptions)moduleClass
    .GetMethod("defaultOptions", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
    .Invoke(null, null);

var dir = "D:/SSCADRepo/URSA.LicenseUsage/docs/dashspec";

foreach (var spec in new[] { "lus-dev-versions.ds", "lus-dev-overview.ds" })
{
    var text = File.ReadAllText(Path.Combine(dir, spec));
    try
    {
        var doc = DashboardComposer.parse(text, Microsoft.FSharp.Core.FSharpOption<string>.Some(dir), options);
        Console.WriteLine($"== {spec}: presets=[{string.Join(", ", doc.ResolvedChartChromePresets.Keys.OrderBy(k => k))}]");
        foreach (var card in doc.Cards)
        {
            var mp = DashSpec.Execution.Runtime.MatrixPresentation.FromCard(card);
            Console.WriteLine($"  {card.Id}: align={mp.Align ?? "<null>"} primaryUse={card.Presentation?.Value?.UsePreset?.Value ?? "<none>"} height={mp.HeightPx}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"== {spec}: FAIL {ex.GetType().Name}: {ex.Message}");
    }
}
