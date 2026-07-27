using System.Text.RegularExpressions;

namespace SuperiorAes.Android.Services;

public sealed record CredentialMigrationResult(
    bool FileFound,
    int ImportedCount,
    string? Error = null);

/// <summary>
/// Migrates a distributor-provisioned plaintext credential file into Android
/// Secure Storage without ever returning, displaying, or logging a value.
/// </summary>
public sealed partial class CredentialMigrationService
{
    public const string LocalFileName = "credentials.local.txt";

    private readonly ICompanionSession _session;
    private readonly SemaphoreSlim _migrationGate = new(1, 1);

    public CredentialMigrationService(ICompanionSession session)
    {
        _session = session;
    }

    public string AppDataFilePath =>
        Path.Combine(FileSystem.Current.AppDataDirectory, LocalFileName);

    public async Task<CredentialMigrationResult> TryMigrateAppDataFileAsync(
        CancellationToken cancellationToken = default)
    {
        await _migrationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(AppDataFilePath))
            {
                return new CredentialMigrationResult(false, 0);
            }

            try
            {
                await using var stream = File.OpenRead(AppDataFilePath);
                var imported = await ImportCoreAsync(stream, cancellationToken).ConfigureAwait(false);

                // This is an app-private staging copy, not the distributor's
                // external source file. Remove it after migration so populated
                // values are not left behind in plaintext.
                File.Delete(AppDataFilePath);
                _session.RecordActivity(
                    $"Automatic credential-file migration completed · {imported} value(s) secured · plaintext staging copy removed");
                return new CredentialMigrationResult(true, imported);
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                _session.RecordActivity("Automatic credential-file migration failed; no credential values were logged");
                return new CredentialMigrationResult(true, 0, exception.Message);
            }
        }
        finally
        {
            _migrationGate.Release();
        }
    }

    public async Task<int> ImportSelectedFileAsync(
        Stream source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        await _migrationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await ImportCoreAsync(source, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _migrationGate.Release();
        }
    }

    private async Task<int> ImportCoreAsync(
        Stream source,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(source, leaveOpen: true);
        var imported = 0;
        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            var separator = trimmed.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var name = trimmed[..separator].Trim();
            var value = trimmed[(separator + 1)..].Trim();
            if (!ValidNameRegex().IsMatch(name) ||
                value.Length == 0 ||
                value == "***")
            {
                continue;
            }

            // Register before any other operation so a subsequent platform
            // exception cannot cause the value to appear in session output.
            _session.RegisterSensitiveValue(value);
            await SecureStorage.Default.SetAsync($"Credential:{name}", value);
            if (string.Equals(name, "GeoapifyApiKey", StringComparison.OrdinalIgnoreCase))
            {
                await SecureStorage.Default.SetAsync("GeoapifyApiKey", value);
            }

            imported++;
        }

        return imported;
    }

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_.-]{0,79}$", RegexOptions.CultureInvariant)]
    private static partial Regex ValidNameRegex();
}
