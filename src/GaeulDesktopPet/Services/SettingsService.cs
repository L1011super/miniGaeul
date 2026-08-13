using System.Text.Json;
using GaeulDesktopPet.Models;

namespace GaeulDesktopPet.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public string SettingsPath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GaeulDesktopPet", "settings.json");

    public PetSettings Load()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        if (!File.Exists(SettingsPath)) return new PetSettings();
        try
        {
            var settings = JsonSerializer.Deserialize<PetSettings>(File.ReadAllText(SettingsPath), JsonOptions) ?? new PetSettings();
            settings.Validate();
            return settings;
        }
        catch (Exception ex)
        {
            LogService.Error("Settings load failed; backing up corrupt file", ex);
            File.Copy(SettingsPath, SettingsPath + ".corrupt-" + DateTime.Now.ToString("yyyyMMddHHmmss"), true);
            return new PetSettings();
        }
    }

    public void Save(PetSettings settings)
    {
        settings.Validate();
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
    }
}
