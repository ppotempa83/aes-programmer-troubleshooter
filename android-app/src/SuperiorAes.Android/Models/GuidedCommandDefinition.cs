using SuperiorAes.Core.Protocol;

namespace SuperiorAes.Android.Models;

public sealed record GuidedCommandDefinition(
    AesCommand? Command,
    string Title,
    string Explanation,
    string EntryFormat,
    string Example,
    bool IsSafetyCritical = false);

public static class GuidedCommandCatalog
{
    public static IReadOnlyList<GuidedCommandDefinition> All { get; } =
        AesCommands.Guides
            .Select(guide => new GuidedCommandDefinition(
                guide.Command,
                guide.Title,
                guide.Explanation,
                guide.EntryFormat,
                guide.Example,
                guide.Command is AesCommand.KeyTransmitter or AesCommand.ProgramIdCipher or AesCommand.ResetRam))
            .Append(new GuidedCommandDefinition(
                null,
                "Contact ID — IntelliPro and IntelliTap",
                "Opens setup, worksheet, interactive 7794 IntelliPro controls, and historical 7067 IntelliTap guidance.",
                "Select the exact accessory and verify its approved subscriber connection before programming.",
                "Legacy 7744F/7788F: 7794 IntelliPro Fire is preferred; 7067 IntelliTap II is historical.",
                true))
            .ToArray();
}
