using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using XboxControllerStudio.ViewModels;

namespace XboxControllerStudio.Services;

/// <summary>
/// Persists application settings to a JSON file in the user's AppData\Local folder.
/// The file is written every time a setting changes and read once on startup.
/// </summary>
public sealed class AppSettingsService
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "XboxControllerStudio",
        "settings.json");

    private sealed class SettingsData
    {
        [JsonPropertyName("selectedLanguageCode")]
        public string SelectedLanguageCode { get; set; } = "en-US";

        [JsonPropertyName("lowBatteryNotificationsEnabled")]
        public bool LowBatteryNotificationsEnabled { get; set; } = true;

        [JsonPropertyName("lowBatteryThresholdPercent")]
        public int LowBatteryThresholdPercent { get; set; } = 20;

        [JsonPropertyName("minimizeToTray")]
        public bool MinimizeToTray { get; set; }

        [JsonPropertyName("pollingIntervalMs")]
        public int PollingIntervalMs { get; set; } = 8;
    }

    public void Load(SettingsViewModel vm)
    {
        if (!File.Exists(FilePath))
            return;

        try
        {
            string json = File.ReadAllText(FilePath);
            var data = JsonSerializer.Deserialize<SettingsData>(json);
            if (data is null)
                return;

            vm.SelectedLanguageCode = data.SelectedLanguageCode;
            vm.LowBatteryNotificationsEnabled = data.LowBatteryNotificationsEnabled;
            vm.LowBatteryThresholdPercent = data.LowBatteryThresholdPercent;
            vm.MinimizeToTray = data.MinimizeToTray;
            vm.PollingIntervalMs = data.PollingIntervalMs;
        }
        catch
        {
            // Corrupt or unreadable settings — silently keep defaults.
        }
    }

    public void Save(SettingsViewModel vm)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);

            var data = new SettingsData
            {
                SelectedLanguageCode = vm.SelectedLanguageCode,
                LowBatteryNotificationsEnabled = vm.LowBatteryNotificationsEnabled,
                LowBatteryThresholdPercent = vm.LowBatteryThresholdPercent,
                MinimizeToTray = vm.MinimizeToTray,
                PollingIntervalMs = vm.PollingIntervalMs
            };

            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }
        catch
        {
            // Non-critical; ignore write failures.
        }
    }
}
