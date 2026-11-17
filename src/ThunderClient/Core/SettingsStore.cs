using System.Text.Json;

namespace ThunderClient.Core;

public sealed class SettingsStore
{
    private readonly string _file = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ThunderClient", "settings.json");
    public ClientSettings Load()
    {
        try
        {
            return File.Exists(_file)
                ? JsonSerializer.Deserialize<ClientSettings>(File.ReadAllText(_file)) ?? new()
                : new();
        }
        catch (JsonException) { return new(); }
        catch (IOException) { return new(); }
    }
    public async Task SaveAsync(ClientSettings settings, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        var temp = _file + ".tmp";
        await File.WriteAllTextAsync(temp,
            JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }), ct);
        File.Move(temp, _file, overwrite: true);
    }
}

