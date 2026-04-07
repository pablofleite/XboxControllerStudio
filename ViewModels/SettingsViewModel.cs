using XboxControllerStudio.Core;

namespace XboxControllerStudio.ViewModels;

/// <summary>
/// Application-level settings exposed to the Settings page.
/// Extend with polling interval, theme selection, startup options, etc.
/// </summary>
public sealed class SettingsViewModel : ObservableObject
{
    public sealed record UiLanguageOption(string Code, string DisplayName);

    public IReadOnlyList<UiLanguageOption> SupportedLanguages { get; } = new[]
    {
        new UiLanguageOption("en-US", "English (US)"),
        new UiLanguageOption("pt-BR", "Portuguese (Brazil)")
    };

    private string _selectedLanguageCode = "en-US";
    public string SelectedLanguageCode
    {
        get => _selectedLanguageCode;
        set => SetProperty(ref _selectedLanguageCode, value);
    }

    private bool _lowBatteryNotificationsEnabled = true;
    public bool LowBatteryNotificationsEnabled
    {
        get => _lowBatteryNotificationsEnabled;
        set => SetProperty(ref _lowBatteryNotificationsEnabled, value);
    }

    private int _lowBatteryThresholdPercent = 20;
    /// <summary>Battery percentage threshold used to notify low battery.</summary>
    public int LowBatteryThresholdPercent
    {
        get => _lowBatteryThresholdPercent;
        set => SetProperty(ref _lowBatteryThresholdPercent, Math.Clamp(value, 5, 90));
    }

    private bool _startWithWindows;
    public bool StartWithWindows
    {
        get => _startWithWindows;
        set => SetProperty(ref _startWithWindows, value);
    }

    private bool _minimizeToTray;
    public bool MinimizeToTray
    {
        get => _minimizeToTray;
        set => SetProperty(ref _minimizeToTray, value);
    }

    private int _pollingIntervalMs = 8;
    /// <summary>Polling interval in milliseconds. Lower = more responsive, higher CPU.</summary>
    public int PollingIntervalMs
    {
        get => _pollingIntervalMs;
        set => SetProperty(ref _pollingIntervalMs, Math.Clamp(value, 1, 100));
    }
}
