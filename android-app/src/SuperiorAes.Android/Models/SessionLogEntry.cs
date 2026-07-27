using System.Globalization;

namespace SuperiorAes.Android.Models;

public sealed record SessionLogEntry(
    DateTimeOffset RecordedAt,
    string Channel,
    string Message)
{
    public string Formatted =>
        $"{RecordedAt.LocalDateTime.ToString("[MM-dd-yyyy / hh:mm (tt)]", CultureInfo.InvariantCulture)} " +
        $"{Channel}: {Message}";
}
