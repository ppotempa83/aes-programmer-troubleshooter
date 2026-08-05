using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;

namespace SuperiorAes.Core.Reporting;

public sealed record SessionExportContext(
    DateTimeOffset SessionStarted,
    DateTimeOffset ExportedAt,
    string SubscriberId,
    string Model,
    string ComputerName,
    string UserName,
    string Connection,
    string TerminalDump,
    Guid? SessionId = null);

public static class SessionExportService
{
    public static string BuildText(SessionExportContext context)
    {
        var builder = new StringBuilder();
        builder.AppendLine("AES PROGRAMMER & TROUBLESHOOTER - COMPLETE SESSION TERMINAL DUMP");
        builder.AppendLine($"Session ID: {SessionId(context)}");
        builder.AppendLine($"Subscriber ID: {Value(context.SubscriberId)}");
        builder.AppendLine($"Model: {Value(context.Model)}");
        builder.AppendLine($"Computer: {Value(context.ComputerName)}");
        builder.AppendLine($"Computer user: {Value(context.UserName)}");
        builder.AppendLine($"Connection: {Value(context.Connection)}");
        builder.AppendLine($"Session started: {Stamp(context.SessionStarted)}");
        builder.AppendLine($"Exported: {Stamp(context.ExportedAt)}");
        builder.AppendLine("Security: system cipher and API keys are excluded/redacted.");
        builder.AppendLine(new string('=', 78));
        builder.Append(context.TerminalDump);
        return builder.ToString();
    }

    public static async Task WriteSpreadsheetAsync(
        string path,
        SessionExportContext context,
        CancellationToken cancellationToken = default)
    {
        await using var stream = File.Create(path);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false);
        WriteEntry(archive, "[Content_Types].xml", ContentTypes);
        WriteEntry(archive, "_rels/.rels", RootRelationships);
        WriteEntry(archive, "xl/workbook.xml", Workbook);
        WriteEntry(archive, "xl/_rels/workbook.xml.rels", WorkbookRelationships);
        WriteEntry(archive, "xl/styles.xml", Styles);
        WriteEntry(archive, "xl/worksheets/sheet1.xml", BuildSheet(context));
        await stream.FlushAsync(cancellationToken);
    }

    private static string BuildSheet(SessionExportContext context)
    {
        var rows = new List<IReadOnlyList<string>>
        {
            new[] { "AES Programmer & Troubleshooter Session Export" },
            new[] { "Session ID", SessionId(context) },
            new[] { "Subscriber ID", Value(context.SubscriberId) },
            new[] { "Model", Value(context.Model) },
            new[] { "Computer", Value(context.ComputerName) },
            new[] { "Computer user", Value(context.UserName) },
            new[] { "Connection", Value(context.Connection) },
            new[] { "Session started", Stamp(context.SessionStarted) },
            new[] { "Exported", Stamp(context.ExportedAt) },
            new[] { "Security", "System cipher and API keys are excluded/redacted." },
            Array.Empty<string>(),
            new[] { "Terminal entry" }
        };
        rows.AddRange(
            context.TerminalDump
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Split('\n')
                .Select(line => (IReadOnlyList<string>)new[] { line }));

        var sheet = new StringBuilder(
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
            "<cols><col min=\"1\" max=\"1\" width=\"110\" customWidth=\"1\"/><col min=\"2\" max=\"2\" width=\"48\" customWidth=\"1\"/></cols><sheetData>");
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            sheet.Append("<row r=\"").Append(rowIndex + 1).Append("\">");
            for (var columnIndex = 0; columnIndex < rows[rowIndex].Count; columnIndex++)
            {
                var reference = $"{(char)('A' + columnIndex)}{rowIndex + 1}";
                sheet.Append("<c r=\"").Append(reference).Append("\" t=\"inlineStr\"");
                if (rowIndex == 0 || rowIndex == 11)
                {
                    sheet.Append(" s=\"1\"");
                }
                sheet.Append("><is><t xml:space=\"preserve\">")
                    .Append(SecurityElement.Escape(SanitizeXml(rows[rowIndex][columnIndex])) ?? string.Empty)
                    .Append("</t></is></c>");
            }
            sheet.Append("</row>");
        }
        sheet.Append("</sheetData></worksheet>");
        return sheet.ToString();
    }

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string Value(string value) => string.IsNullOrWhiteSpace(value) ? "Not available" : value;
    private static string SessionId(SessionExportContext context) =>
        context.SessionId?.ToString("D") ?? "Not assigned";
    private static string SanitizeXml(string value) =>
        new(value.Where(character =>
            character is '\t' or '\n' or '\r' ||
            character >= ' ' && character is not '\uFFFE' and not '\uFFFF').ToArray());

    private static string Stamp(DateTimeOffset value) =>
        value.LocalDateTime.ToString("[MM-dd-yyyy / hh:mm (tt)]", CultureInfo.InvariantCulture);

    private const string ContentTypes =
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/><Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/></Types>";
    private const string RootRelationships =
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>";
    private const string Workbook =
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Session Log\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>";
    private const string WorkbookRelationships =
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/><Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/></Relationships>";
    private const string Styles =
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font><font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts><fills count=\"1\"><fill><patternFill patternType=\"none\"/></fill></fills><borders count=\"1\"><border/></borders><cellStyleXfs count=\"1\"><xf/></cellStyleXfs><cellXfs count=\"2\"><xf fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/><xf fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/></cellXfs></styleSheet>";
}
