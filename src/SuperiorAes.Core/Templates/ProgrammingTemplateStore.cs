using System.Text.Json;
using SuperiorAes.Core.Models;

namespace SuperiorAes.Core.Templates;

public sealed class ProgrammingTemplateStore
{
    private readonly string _path;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public ProgrammingTemplateStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
    }

    public async Task<IReadOnlyList<ProgrammingTemplate>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path))
        {
            return ProgrammingTemplate.Defaults;
        }

        await using var stream = File.OpenRead(_path);
        var templates = await JsonSerializer.DeserializeAsync<List<ProgrammingTemplate>>(
            stream,
            JsonOptions,
            cancellationToken);
        return templates is { Count: > 0 } ? templates : ProgrammingTemplate.Defaults;
    }

    public async Task SaveAsync(
        IEnumerable<ProgrammingTemplate> templates,
        CancellationToken cancellationToken = default)
    {
        var values = templates
            .OrderBy(template => template.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(_path);
        await JsonSerializer.SerializeAsync(stream, values, JsonOptions, cancellationToken);
    }
}
