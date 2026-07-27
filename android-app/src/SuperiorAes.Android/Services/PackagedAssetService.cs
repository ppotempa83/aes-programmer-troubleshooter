namespace SuperiorAes.Android.Services;

public static class PackagedAssetService
{
    public static async Task<string> MaterializeAsync(
        string logicalName,
        CancellationToken cancellationToken = default)
    {
        var fileName = Path.GetFileName(logicalName);
        var directory = Path.Combine(FileSystem.Current.AppDataDirectory, "PackagedDocuments");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);

        await using var source = await FileSystem.Current.OpenAppPackageFileAsync(logicalName);
        await using var destination = File.Create(path);
        await source.CopyToAsync(destination, cancellationToken);
        return path;
    }

    public static async Task<string> ReadTextAsync(
        string logicalName,
        CancellationToken cancellationToken = default)
    {
        await using var stream = await FileSystem.Current.OpenAppPackageFileAsync(logicalName);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    public static async Task<ImageSource> LoadImageAsync(
        string logicalName,
        CancellationToken cancellationToken = default)
    {
        await using var stream = await FileSystem.Current.OpenAppPackageFileAsync(logicalName);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();
        return ImageSource.FromStream(() => new MemoryStream(bytes, writable: false));
    }
}
