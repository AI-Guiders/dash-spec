using DashSpec.Core.Model;

namespace DashSpec.Core.Parsing;

public static class HostModuleParser
{
    public static HostDocument Parse(string text, string? specDirectory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if (HostParseBridge.Parse is { } parse)
        {
            return parse(text, specDirectory);
        }

        throw new InvalidOperationException("Host parse bridge not registered.");
    }

    public static HostDocument ParseFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Host file not found.", path);
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        return Parse(File.ReadAllText(path), directory);
    }
}
