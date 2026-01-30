using System.IO;
using System.Text.Json;

namespace iVillager;

public class AppSettings
{
    private static readonly string SettingsPath = Path.Combine(
        AppContext.BaseDirectory,
        "app_settings.json");

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public string HotkeyStartStop { get; set; } = "Ctrl+Shift+F1";

    public static AppSettings Load()
    {
        if (!File.Exists(SettingsPath))
            return new AppSettings();

        try
        {
            var json = File.ReadAllText(SettingsPath);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json, Options);
            return loaded ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(this, Options);
        File.WriteAllText(SettingsPath, json);
    }
}
