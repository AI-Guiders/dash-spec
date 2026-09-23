using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;

namespace DashSpec.Core.Data;

/// <summary>Writes a one-sheet xlsx for card export. Reading xlsx is SQL Server OPENROWSET.</summary>
public static class XlsxWorkbook
{
    public static byte[] Write(IReadOnlyList<string> columns, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var shared = new List<string>();
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        int Share(string? text)
        {
            text ??= string.Empty;
            if (!index.TryGetValue(text, out var at))
            {
                at = shared.Count;
                shared.Add(text);
                index[text] = at;
            }

            return at;
        }

        var sheet = new XElement(Ns + "sheetData");
        sheet.Add(BuildRow(1, columns.Select(column => (object)Share(column)).ToList(), sharedString: true));
        for (var r = 0; r < rows.Count; r++)
        {
            var values = new List<object>();
            var source = rows[r];
            for (var c = 0; c < columns.Count; c++)
            {
                values.Add(Share(c < source.Count ? source[c] : null));
            }

            sheet.Add(BuildRow(r + 2, values, sharedString: true));
        }

        var worksheet = new XDocument(
            new XElement(Ns + "worksheet",
                new XAttribute(XNamespace.Xmlns + "r", "http://schemas.openxmlformats.org/officeDocument/2006/relationships"),
                sheet));

        var sharedDoc = new XDocument(
            new XElement(Ns + "sst",
                new XAttribute("count", shared.Count),
                new XAttribute("uniqueCount", shared.Count),
                shared.Select(text => new XElement(Ns + "si", new XElement(Ns + "t", text)))));

        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(zip, "[Content_Types].xml", ContentTypes);
            WriteEntry(zip, "_rels/.rels", RootRels);
            WriteEntry(zip, "xl/workbook.xml", Workbook);
            WriteEntry(zip, "xl/_rels/workbook.xml.rels", WorkbookRels);
            WriteEntry(zip, "xl/worksheets/sheet1.xml", worksheet.ToString(SaveOptions.DisableFormatting));
            WriteEntry(zip, "xl/sharedStrings.xml", sharedDoc.ToString(SaveOptions.DisableFormatting));
        }

        return buffer.ToArray();
    }

    private static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    private static XElement BuildRow(int rowNumber, IReadOnlyList<object> values, bool sharedString)
    {
        var row = new XElement(Ns + "row", new XAttribute("r", rowNumber));
        for (var i = 0; i < values.Count; i++)
        {
            var reference = ColumnName(i) + rowNumber.ToString(CultureInfo.InvariantCulture);
            var cell = new XElement(Ns + "c", new XAttribute("r", reference));
            if (sharedString)
            {
                cell.Add(new XAttribute("t", "s"));
            }

            cell.Add(new XElement(Ns + "v", Convert.ToString(values[i], CultureInfo.InvariantCulture)));
            row.Add(cell);
        }

        return row;
    }

    private static string ColumnName(int index)
    {
        var name = string.Empty;
        var n = index + 1;
        while (n > 0)
        {
            n--;
            name = (char)('A' + (n % 26)) + name;
            n /= 26;
        }

        return name;
    }

    private static void WriteEntry(ZipArchive zip, string name, string contents)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(contents);
    }

    private const string ContentTypes =
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
          <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
          <Default Extension="xml" ContentType="application/xml"/>
          <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
          <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
          <Override PartName="/xl/sharedStrings.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml"/>
        </Types>
        """;

    private const string RootRels =
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
        </Relationships>
        """;

    private const string Workbook =
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
          <sheets><sheet name="Export" sheetId="1" r:id="rId1"/></sheets>
        </workbook>
        """;

    private const string WorkbookRels =
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
          <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings" Target="sharedStrings.xml"/>
        </Relationships>
        """;
}
