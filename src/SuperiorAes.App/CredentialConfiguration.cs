using System.IO;

namespace SuperiorAes.App;

internal static class CredentialConfiguration
{
    private const string Placeholder = "***";

    public static string ReadGeoapifyApiKey()
    {
        var environmentValue = Environment.GetEnvironmentVariable("GEOAPIFY_API_KEY");
        if (IsUsable(environmentValue))
        {
            return environmentValue!.Trim();
        }

        foreach (var path in CandidatePaths())
        {
            var value = ReadValue(path, "GeoapifyApiKey");
            if (IsUsable(value))
            {
                return value!.Trim();
            }
        }

        return string.Empty;
    }

    public static string PreferredLocalPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "SuperiorFire",
            "AES Programmer",
            "credentials.local.txt");

    private static IEnumerable<string> CandidatePaths()
    {
        yield return PreferredLocalPath;
        yield return Path.Combine(AppContext.BaseDirectory, "config", "credentials.local.txt");
        yield return Path.Combine(AppContext.BaseDirectory, "credentials.local.txt");
    }

    private static string? ReadValue(string path, string key)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            foreach (var line in File.ReadLines(path))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                {
                    continue;
                }

                var separator = trimmed.IndexOf('=');
                if (separator <= 0 ||
                    !string.Equals(trimmed[..separator].Trim(), key, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return trimmed[(separator + 1)..].Trim();
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Credential values are optional. Startup must remain usable in simulation mode.
        }

        return null;
    }

    private static bool IsUsable(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        !string.Equals(value.Trim(), Placeholder, StringComparison.Ordinal);
}
