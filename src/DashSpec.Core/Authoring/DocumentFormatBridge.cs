namespace DashSpec.Core.Authoring;

internal static class DocumentFormatBridge
{
    public static Func<string, EditorConfig.EditorConfigOptions, string>? FormatDashSpec;
}
