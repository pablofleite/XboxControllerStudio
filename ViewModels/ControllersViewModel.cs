using System.Collections.ObjectModel;
using System.Windows;
using XboxControllerStudio.Core;
using XboxControllerStudio.Services;

namespace XboxControllerStudio.ViewModels;

/// <summary>
/// Exposes the list of controller slots and tracks which slot is selected.
/// Subscribes to InputPollingService and routes each state update
/// to the correct ControllerViewModel.
/// </summary>
public sealed class ControllersViewModel : ObservableObject
{
    private const int NotifyResetHysteresisPercent = 5;

    // Four fixed slots (XInput supports 0–3)
    public ObservableCollection<ControllerViewModel> Controllers { get; } = new();
    private readonly SettingsViewModel _settings;
    private readonly bool[] _lowBatteryNotified = new bool[4];

    private ControllerViewModel? _selected;
    public ControllerViewModel? SelectedController
    {
        get => _selected;
        set => SetProperty(ref _selected, value);
    }

    public event Action<string>? LowBatteryAlertRaised;

    public void ApplyProfileToAll(Models.MappingProfile profile)
    {
        foreach (var controller in Controllers)
            controller.ApplyProfile(profile);
    }

    public void RefreshLocalization()
    {
        foreach (var controller in Controllers)
            controller.RefreshLocalization();
    }

    public ControllersViewModel(InputPollingService polling, SendInputService sendInput, SettingsViewModel settings)
    {
        _settings = settings;

        for (int i = 0; i < 4; i++)
            Controllers.Add(new ControllerViewModel(i, sendInput));

        SelectedController = Controllers[0];

        // Subscribe once; routing is done by playerIndex
        polling.StateUpdated += OnStateUpdated;
    }

    private void OnStateUpdated(Models.ControllerState state)
    {
        CheckLowBattery(state);

        // Each ControllerViewModel handles its own thread marshalling
        Controllers[state.PlayerIndex].OnStateReceived(state);
    }

    private void CheckLowBattery(Models.ControllerState state)
    {
        int idx = state.PlayerIndex;

        if (!state.IsConnected || state.IsWired || !state.HasBattery)
        {
            _lowBatteryNotified[idx] = false;
            return;
        }

        int threshold = _settings.LowBatteryThresholdPercent;
        bool isLow = state.BatteryPercent <= threshold;

        if (!isLow)
        {
            // Hysteresis avoids notification flapping around exact threshold.
            if (state.BatteryPercent >= threshold + NotifyResetHysteresisPercent)
                _lowBatteryNotified[idx] = false;
            return;
        }

        if (!_settings.LowBatteryNotificationsEnabled || _lowBatteryNotified[idx])
            return;

        _lowBatteryNotified[idx] = true;

        string format = GetString("LowBatteryAlert", "Controller {0}: low battery level.");
        string message = string.Format(format, idx + 1);
        Application.Current?.Dispatcher.InvokeAsync(() => LowBatteryAlertRaised?.Invoke(message));
    }

    private static string GetString(string key, string fallback)
    {
        if (Application.Current?.TryFindResource(key) is string text && !string.IsNullOrWhiteSpace(text))
            return text;

        return fallback;
    }
}
