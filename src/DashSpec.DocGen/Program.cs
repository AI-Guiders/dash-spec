using System.Reflection;
using System.Text;
using System.Xml.Linq;
using DashSpec.Core.Authoring;

var repoRoot = ResolveRepoRoot(args);
var coreXml = Path.Combine(repoRoot, "src/DashSpec.Core/bin/Release/net10.0/DashSpec.Core.xml");
if (!File.Exists(coreXml))
{
    coreXml = Path.Combine(repoRoot, "src/DashSpec.Core/bin/Debug/net10.0/DashSpec.Core.xml");
}

if (!File.Exists(coreXml))
{
    Console.Error.WriteLine("Build DashSpec.Core first (Release or Debug) to produce DashSpec.Core.xml");
    return 1;
}

var summaries = LoadSummaries(coreXml);
var topics = typeof(AuthoringCatalog)
    .GetNestedTypes(BindingFlags.Public | BindingFlags.Static)
    .Select(t => new
    {
        Type = t,
        Attr = t.GetCustomAttribute<AuthoringTopicAttribute>(),
    })
    .Where(x => x.Attr is not null)
    .OrderBy(x => x.Attr!.Order)
    .ToList();

var outputDir = Path.Combine(repoRoot, "docs/authoring/generated");
Directory.CreateDirectory(outputDir);

var sb = new StringBuilder();
sb.AppendLine("# DashSpec — справочник авторинга");
sb.AppendLine();
sb.AppendLine("> Сгенерировано из XML-doc (`AuthoringCatalog` + парсеры). Не редактировать вручную.");
sb.AppendLine($"> Команда: `dotnet run --project src/DashSpec.DocGen`");
sb.AppendLine();

foreach (var topic in topics)
{
    var typeName = topic.Type.FullName!.Replace('+', '.');
    var key = $"T:{typeName}";
    if (!summaries.TryGetValue(key, out var body) || string.IsNullOrWhiteSpace(body))
    {
        Console.Error.WriteLine($"Missing XML doc for {topic.Type.Name}");
        continue;
    }

    sb.AppendLine(body.Trim());
    sb.AppendLine();
    sb.AppendLine("---");
    sb.AppendLine();
}

var outPath = Path.Combine(outputDir, "AUTHORING.md");
File.WriteAllText(outPath, sb.ToString().TrimEnd() + Environment.NewLine, Encoding.UTF8);
Console.WriteLine($"Wrote {outPath}");
return 0;

static string ResolveRepoRoot(string[] args)
{
    if (args.Length > 0 && Directory.Exists(args[0]))
    {
        return Path.GetFullPath(args[0]);
    }

    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null)
    {
        if (File.Exists(Path.Combine(dir.FullName, "DashSpec.slnx")))
        {
            return dir.FullName;
        }

        dir = dir.Parent;
    }

    throw new InvalidOperationException("Could not locate dash-spec repo root.");
}

static Dictionary<string, string> LoadSummaries(string xmlPath)
{
    var doc = XDocument.Load(xmlPath);
    var map = new Dictionary<string, string>(StringComparer.Ordinal);
    foreach (var member in doc.Root?.Elements("members").Elements("member") ?? [])
    {
        var name = member.Attribute("name")?.Value;
        var summary = member.Element("summary")?.Value;
        if (name is not null && summary is not null)
        {
            map[name] = summary.Trim();
        }
    }

    return map;
}
