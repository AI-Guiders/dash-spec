namespace DashSpec.Execution.Compilation.Dialects;

/// <summary>
/// xlsx datasource as SQL Server <c>OPENROWSET</c> (ACE OLEDB).
/// The SQL Server service account must be able to read the file.
/// Requires Ad Hoc Distributed Queries and the Access Database Engine provider.
/// </summary>
internal static class TSqlXlsxOpenRowSet
{
    internal const string Provider = "Microsoft.ACE.OLEDB.12.0";

    public static string Format(string absolutePath, string? sheet)
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
        {
            throw new InvalidOperationException("xlsx datasource path is empty.");
        }

        if (absolutePath.Contains('\'') || absolutePath.Contains(';') || absolutePath.IndexOfAny(['\r', '\n']) >= 0)
        {
            throw new InvalidOperationException(
                "xlsx path cannot contain quotes, semicolons, or line breaks (it is embedded in OPENROWSET).");
        }

        var sheetName = string.IsNullOrWhiteSpace(sheet) ? "Sheet1" : sheet.Trim();
        if (sheetName.EndsWith('$'))
        {
            sheetName = sheetName[..^1];
        }

        if (sheetName.Length == 0 || sheetName.Contains('\'') || sheetName.IndexOfAny(['\r', '\n']) >= 0)
        {
            throw new InvalidOperationException("xlsx sheet name is empty or contains quotes.");
        }

        var escapedSheet = sheetName.Replace("]", "]]", StringComparison.Ordinal);
        return
            $"OPENROWSET('{Provider}', " +
            $"'Excel 12.0 Xml;HDR=YES;IMEX=1;Database={absolutePath}', " +
            $"'SELECT * FROM [{escapedSheet}$]') AS xlsx_src";
    }

    public static string ResolvePath(string relativeOrAbsolute, string? specDirectory)
    {
        if (Path.IsPathRooted(relativeOrAbsolute))
        {
            return Path.GetFullPath(relativeOrAbsolute);
        }

        var root = string.IsNullOrWhiteSpace(specDirectory)
            ? Directory.GetCurrentDirectory()
            : specDirectory;
        return Path.GetFullPath(Path.Combine(root, relativeOrAbsolute.Replace('/', Path.DirectorySeparatorChar)));
    }
}
