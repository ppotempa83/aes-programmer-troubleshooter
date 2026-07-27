using System.IO.Compression;
using SuperiorAes.Core.Reporting;

namespace SuperiorAes.Core.Tests;

public sealed class SessionExportTests
{
    [Fact]
    public void TextExportIncludesSessionMetadataAndTerminalDump()
    {
        var context = Context("[07-27-2026 / 03:45 (PM)] RX · ID#:1A2B");

        var text = SessionExportService.BuildText(context);

        Assert.Contains("Session ID: 75d39814-067f-4fc2-9f06-bd16d4f12cd8", text);
        Assert.Contains("Subscriber ID: 1A2B", text);
        Assert.Contains("Computer: TEST-PC", text);
        Assert.Contains("Computer user: technician", text);
        Assert.Contains("Session started: [07-27-2026 / 03:30 (PM)]", text);
        Assert.Contains("[07-27-2026 / 03:45 (PM)] RX · ID#:1A2B", text);
        Assert.Contains("API keys are excluded/redacted", text);
    }

    [Fact]
    public async Task SpreadsheetExportCreatesReadableXlsxPackage()
    {
        var path = Path.Combine(Path.GetTempPath(), $"aes-session-{Guid.NewGuid():N}.xlsx");
        try
        {
            await SessionExportService.WriteSpreadsheetAsync(path, Context("[07-27-2026 / 03:45 (PM)] APP · TEST"));

            using var archive = ZipFile.OpenRead(path);
            Assert.Contains(archive.Entries, entry => entry.FullName == "xl/workbook.xml");
            var sheetEntry = Assert.Single(archive.Entries, entry => entry.FullName == "xl/worksheets/sheet1.xml");
            using var reader = new StreamReader(sheetEntry.Open());
            var sheet = await reader.ReadToEndAsync();
            Assert.Contains("Subscriber ID", sheet);
            Assert.Contains("1A2B", sheet);
            Assert.Contains("75d39814-067f-4fc2-9f06-bd16d4f12cd8", sheet);
            Assert.Contains("APP · TEST", sheet);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static SessionExportContext Context(string terminalDump) =>
        new(
            new DateTimeOffset(2026, 7, 27, 15, 30, 0, TimeSpan.FromHours(-5)),
            new DateTimeOffset(2026, 7, 27, 15, 45, 0, TimeSpan.FromHours(-5)),
            "1A2B",
            "7788F",
            "TEST-PC",
            "technician",
            "SIM-7788F",
            terminalDump,
            Guid.Parse("75d39814-067f-4fc2-9f06-bd16d4f12cd8"));
}
