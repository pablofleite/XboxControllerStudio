using System.Windows;
using System.Windows.Input;
using XboxControllerStudio.Core;
using XboxControllerStudio.Core.Input;
using XboxControllerStudio.Core.Mapping;
using XboxControllerStudio.Models;
using XboxControllerStudio.Services;

namespace XboxControllerStudio.ViewModels;

/// <summary>
/// Represents one connected (or disconnected) controller slot.
/// Owns the deadzone processor for that slot and exposes all
/// properties the UI needs to display real-time input state.
/// </summary>
public sealed class ControllerViewModel : ObservableObject
{
    private const int DeadzoneCalibrationDurationMs = 2500;
    private const float DeadzoneCalibrationMargin = 0.03f;
    private const float DeadzoneCalibrationMin = 0.02f;
    private const float DeadzoneCalibrationMax = 0.35f;

    private readonly ButtonMappingProcessor _mapper;
    private readonly SendInputService _sendInput;
    private readonly object _calibrationLock = new();
    private MappingProfile _profile;
    private string _rawConnectionType = "Offline";
    private string _rawBatteryText = "N/A";
    private DateTime _calibrationStartedUtc;
    private float _calibrationLeftMax;
    private float _calibrationRightMax;
    private double _mouseCarryX;
    private double _mouseCarryY;

    public int PlayerIndex { get; }

    // --- Connection ---
    private bool _isConnected;
    public bool IsConnected
    {
        get => _isConnected;
        private set => SetProperty(ref _isConnected, value);
    }

    public string Title => string.Format(GetString("ControllerTitleFormat", "Controller {0}"), PlayerIndex + 1);

    private string _connectionType = "Offline";
    public string ConnectionType
    {
        get => _connectionType;
        private set
        {
            if (SetProperty(ref _connectionType, value))
                OnPropertyChanged(nameof(ConnectionType));
        }
    }

    // --- Battery ---
    private bool _isWired;
    public bool IsWired
    {
        get => _isWired;
        private set => SetProperty(ref _isWired, value);
    }

    private bool _hasBattery;
    public bool HasBattery
    {
        get => _hasBattery;
        private set => SetProperty(ref _hasBattery, value);
    }

    private int _batteryPercent;
    public int BatteryPercent
    {
        get => _batteryPercent;
        private set => SetProperty(ref _batteryPercent, value);
    }

    private string _batteryText = "N/A";
    public string BatteryText
    {
        get => _batteryText;
        private set
        {
            if (SetProperty(ref _batteryText, value))
                OnPropertyChanged(nameof(ConnectionType));
        }
    }

    // --- Buttons ---
    private bool _a, _b, _x, _y, _lb, _rb, _start, _back, _ls, _rs;
    private bool _dpadUp, _dpadDown, _dpadLeft, _dpadRight;
    private bool _lt, _rt;

    public bool A { get => _a; private set => SetProperty(ref _a, value); }
    public bool B { get => _b; private set => SetProperty(ref _b, value); }
    public bool X { get => _x; private set => SetProperty(ref _x, value); }
    public bool Y { get => _y; private set => SetProperty(ref _y, value); }
    public bool LB { get => _lb; private set => SetProperty(ref _lb, value); }
    public bool RB { get => _rb; private set => SetProperty(ref _rb, value); }
    public bool Start { get => _start; private set => SetProperty(ref _start, value); }
    public bool Back { get => _back; private set => SetProperty(ref _back, value); }
    public bool LS { get => _ls; private set => SetProperty(ref _ls, value); }
    public bool RS { get => _rs; private set => SetProperty(ref _rs, value); }
    public bool DPadUp { get => _dpadUp; private set => SetProperty(ref _dpadUp, value); }
    public bool DPadDown { get => _dpadDown; private set => SetProperty(ref _dpadDown, value); }
    public bool DPadLeft { get => _dpadLeft; private set => SetProperty(ref _dpadLeft, value); }
    public bool DPadRight { get => _dpadRight; private set => SetProperty(ref _dpadRight, value); }
    public bool LT { get => _lt; private set => SetProperty(ref _lt, value); }
    public bool RT { get => _rt; private set => SetProperty(ref _rt, value); }

    // --- Analogue ---
    private float _leftTrigger, _rightTrigger;
    private float _leftStickX, _leftStickY, _rightStickX, _rightStickY;

    public float LeftTrigger { get => _leftTrigger; private set => SetProperty(ref _leftTrigger, value); }
    public float RightTrigger { get => _rightTrigger; private set => SetProperty(ref _rightTrigger, value); }
    public float LeftStickX { get => _leftStickX; private set => SetProperty(ref _leftStickX, value); }
    public float LeftStickY { get => _leftStickY; private set => SetProperty(ref _leftStickY, value); }
    public float RightStickX { get => _rightStickX; private set => SetProperty(ref _rightStickX, value); }
    public float RightStickY { get => _rightStickY; private set => SetProperty(ref _rightStickY, value); }

    // --- Controller settings page controls ---
    private float _sensitivity = 1.0f;
    public float Sensitivity
    {
        get => _sensitivity;
        set
        {
            if (SetProperty(ref _sensitivity, Math.Clamp(value, 0.1f, 2.0f)))
                RaisePreviewChanged();
        }
    }

    private bool _vibrationEnabled = true;
    public bool VibrationEnabled
    {
        get => _vibrationEnabled;
        set => SetProperty(ref _vibrationEnabled, value);
    }

    public RelayCommand AutoCalibrateDeadzoneCommand { get; }

    // Realtime preview values (after sensitivity curve for quick tuning feedback)
    public float PreviewLeftStickX => ApplySensitivity(LeftStickX);
    public float PreviewLeftStickY => ApplySensitivity(LeftStickY);
    public float PreviewRightStickX => ApplySensitivity(RightStickX);
    public float PreviewRightStickY => ApplySensitivity(RightStickY);

    // --- Deadzone settings (bound to sliders) ---
    public float InnerDeadzone
    {
        get => (LeftInnerDeadzone + RightInnerDeadzone) * 0.5f;
        set
        {
            LeftInnerDeadzone = value;
            RightInnerDeadzone = value;
        }
    }

    private float _leftInnerDeadzone = 0.12f;
    public float LeftInnerDeadzone
    {
        get => _leftInnerDeadzone;
        set
        {
            float clamped = Math.Clamp(value, 0f, 0.5f);
            if (SetProperty(ref _leftInnerDeadzone, clamped))
            {
                _profile.Deadzone.LeftInnerDeadzone = clamped;
                OnPropertyChanged(nameof(InnerDeadzone));
                OnPropertyChanged(nameof(LeftDeadzoneRadius));
            }
        }
    }

    private float _rightInnerDeadzone = 0.12f;
    public float RightInnerDeadzone
    {
        get => _rightInnerDeadzone;
        set
        {
            float clamped = Math.Clamp(value, 0f, 0.5f);
            if (SetProperty(ref _rightInnerDeadzone, clamped))
            {
                _profile.Deadzone.RightInnerDeadzone = clamped;
                OnPropertyChanged(nameof(InnerDeadzone));
                OnPropertyChanged(nameof(RightDeadzoneRadius));
            }
        }
    }

    public float LeftDeadzoneRadius => LeftInnerDeadzone;
    public float RightDeadzoneRadius => RightInnerDeadzone;

    private bool _isCalibratingDeadzone;
    public bool IsCalibratingDeadzone
    {
        get => _isCalibratingDeadzone;
        private set => SetProperty(ref _isCalibratingDeadzone, value);
    }

    private string _deadzoneCalibrationStatus;
    public string DeadzoneCalibrationStatus
    {
        get => _deadzoneCalibrationStatus;
        private set => SetProperty(ref _deadzoneCalibrationStatus, value);
    }

    public ControllerViewModel(int playerIndex, SendInputService sendInput)
    {
        PlayerIndex = playerIndex;
        _sendInput = sendInput;
        _profile = new MappingProfile { Name = GetString("ProfilesDefaultName", "Default") };
        _mapper = new ButtonMappingProcessor(sendInput);
        _deadzoneCalibrationStatus = GetString("DeadzoneStatusManual", "Manual tuning");
        AutoCalibrateDeadzoneCommand = new RelayCommand(StartAutoDeadzoneCalibration, () => !IsCalibratingDeadzone && IsConnected);
    }

    public void RefreshLocalization()
    {
        OnPropertyChanged(nameof(Title));
        ConnectionType = LocalizeConnectionType(_rawConnectionType);
        BatteryText = LocalizeBatteryText(_rawBatteryText);

        if (IsCalibratingDeadzone)
            DeadzoneCalibrationStatus = GetString("DeadzoneStatusCalibrating", "Calibrating... keep sticks untouched (2.5s)");
        else
            DeadzoneCalibrationStatus = GetString("DeadzoneStatusManual", "Manual tuning");
    }

    /// <summary>
    /// Applies a new profile (e.g. when the user switches profiles).
    /// Resets edge detection to prevent stuck keys.
    /// </summary>
    public void ApplyProfile(MappingProfile profile)
    {
        _profile = profile;
        _mapper.Reset();
        _mouseCarryX = 0;
        _mouseCarryY = 0;
        LeftInnerDeadzone = profile.Deadzone.LeftInnerDeadzone;
        RightInnerDeadzone = profile.Deadzone.RightInnerDeadzone;
    }

    /// <summary>
    /// Called by the polling service on a background thread.
    /// Marshals all property updates to the UI thread.
    /// </summary>
    public void OnStateReceived(ControllerState raw)
    {
        CaptureDeadzoneCalibrationSample(raw);

        // Apply deadzone before updating UI and processing mappings
        var state = DeadzoneProcessor.Apply(raw, _profile.Deadzone);

        // Run mapping before marshalling to UI — no UI access needed here
        if (state.IsConnected)
        {
            _mapper.Process(state, _profile);
            ProcessMouseMovement(state);
        }
        else
        {
            _mapper.Reset();
            _mouseCarryX = 0;
            _mouseCarryY = 0;
        }

        // Marshal to UI thread
        Application.Current?.Dispatcher.InvokeAsync(() => UpdateProperties(state));
    }

    private void StartAutoDeadzoneCalibration()
    {
        lock (_calibrationLock)
        {
            _calibrationLeftMax = 0f;
            _calibrationRightMax = 0f;
            _calibrationStartedUtc = DateTime.UtcNow;
        }

        IsCalibratingDeadzone = true;
        DeadzoneCalibrationStatus = GetString("DeadzoneStatusCalibrating", "Calibrating... keep sticks untouched (2.5s)");
        CommandManager.InvalidateRequerySuggested();
    }

    private void CaptureDeadzoneCalibrationSample(ControllerState raw)
    {
        if (!IsCalibratingDeadzone)
            return;

        bool finishNow = false;
        float leftFinal = 0f;
        float rightFinal = 0f;

        lock (_calibrationLock)
        {
            float leftMag = MathF.Sqrt(raw.LeftStickX * raw.LeftStickX + raw.LeftStickY * raw.LeftStickY);
            float rightMag = MathF.Sqrt(raw.RightStickX * raw.RightStickX + raw.RightStickY * raw.RightStickY);

            _calibrationLeftMax = MathF.Max(_calibrationLeftMax, leftMag);
            _calibrationRightMax = MathF.Max(_calibrationRightMax, rightMag);

            if ((DateTime.UtcNow - _calibrationStartedUtc).TotalMilliseconds >= DeadzoneCalibrationDurationMs)
            {
                leftFinal = Math.Clamp(_calibrationLeftMax + DeadzoneCalibrationMargin, DeadzoneCalibrationMin, DeadzoneCalibrationMax);
                rightFinal = Math.Clamp(_calibrationRightMax + DeadzoneCalibrationMargin, DeadzoneCalibrationMin, DeadzoneCalibrationMax);
                finishNow = true;
            }
        }

        if (!finishNow)
            return;

        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            LeftInnerDeadzone = leftFinal;
            RightInnerDeadzone = rightFinal;
            IsCalibratingDeadzone = false;
            string format = GetString("DeadzoneStatusAutoCalibrated", "Auto calibrated - L: {0:0.00} / R: {1:0.00}");
            DeadzoneCalibrationStatus = string.Format(format, leftFinal, rightFinal);
            CommandManager.InvalidateRequerySuggested();
        });
    }

    private void UpdateProperties(ControllerState s)
    {
        _rawConnectionType = s.ConnectionType;
        _rawBatteryText = s.BatteryText;

        IsConnected = s.IsConnected;
        IsWired = s.IsWired;
        ConnectionType = LocalizeConnectionType(s.ConnectionType);
        HasBattery = s.HasBattery;
        BatteryPercent = s.BatteryPercent;
        BatteryText = LocalizeBatteryText(s.BatteryText);
        CommandManager.InvalidateRequerySuggested();

        if (!s.IsConnected) return;

        A = s.A; B = s.B; X = s.X; Y = s.Y;
        LB = s.LB; RB = s.RB;
        Start = s.Start; Back = s.Back;
        LS = s.LS; RS = s.RS;
        DPadUp = s.DPadUp; DPadDown = s.DPadDown;
        DPadLeft = s.DPadLeft; DPadRight = s.DPadRight;
        LT = s.LT_Digital; RT = s.RT_Digital;

        LeftTrigger = s.LeftTrigger;
        RightTrigger = s.RightTrigger;
        LeftStickX = s.LeftStickX;
        LeftStickY = s.LeftStickY;
        RightStickX = s.RightStickX;
        RightStickY = s.RightStickY;

        RaisePreviewChanged();
    }

    private float ApplySensitivity(float input)
    {
        float scaled = input * Sensitivity;
        return Math.Clamp(scaled, -1f, 1f);
    }

    private void RaisePreviewChanged()
    {
        OnPropertyChanged(nameof(PreviewLeftStickX));
        OnPropertyChanged(nameof(PreviewLeftStickY));
        OnPropertyChanged(nameof(PreviewRightStickX));
        OnPropertyChanged(nameof(PreviewRightStickY));
    }

    private static string LocalizeConnectionType(string raw)
    {
        return raw switch
        {
            "Offline" => GetString("ConnectionTypeOffline", "Offline"),
            "USB" => GetString("ConnectionTypeUsb", "USB"),
            "Bluetooth" => GetString("ConnectionTypeBluetooth", "Bluetooth"),
            "Wireless" => GetString("ConnectionTypeWireless", "Wireless"),
            _ => raw
        };
    }

    private static string LocalizeBatteryText(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return GetString("BatteryTextNotAvailable", "N/A");

        if (raw.EndsWith("%", StringComparison.Ordinal))
            return raw;

        return raw switch
        {
            "Disconnected" => GetString("BatteryTextDisconnected", "Disconnected"),
            "Wired" => GetString("BatteryTextWired", "Wired"),
            "Battery Unavailable" => GetString("BatteryTextUnavailable", "Battery Unavailable"),
            "N/A" => GetString("BatteryTextNotAvailable", "N/A"),
            _ => raw
        };
    }

    private static string GetString(string key, string fallback)
    {
        if (Application.Current?.TryFindResource(key) is string value && !string.IsNullOrWhiteSpace(value))
            return value;

        return fallback;
    }

    private void ProcessMouseMovement(ControllerState state)
    {
        if (!_profile.UseRightStickAsMouse)
            return;

        float sensitivity = Math.Clamp(_profile.RightStickMouseSensitivity, 1f, 40f);
        double moveX = ScaleStickAxis(state.RightStickX) * sensitivity + _mouseCarryX;
        double moveY = -ScaleStickAxis(state.RightStickY) * sensitivity + _mouseCarryY;

        int dx = (int)Math.Truncate(moveX);
        int dy = (int)Math.Truncate(moveY);

        _mouseCarryX = moveX - dx;
        _mouseCarryY = moveY - dy;

        if (dx == 0 && dy == 0)
            return;

        _sendInput.MouseMoveRelative(dx, dy);
    }

    private static double ScaleStickAxis(float axis)
    {
        float abs = MathF.Abs(axis);
        return axis * abs;
    }
}
