namespace DashSpec.Execution.Parsing;

public static class HostModuleParser
{
    public static HostDocument Parse(string text, string? specDirectory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        return ModuleParseRegistration.ParseHost(text, specDirectory);
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
