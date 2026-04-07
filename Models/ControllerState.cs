namespace XboxControllerStudio.Models;

/// <summary>
/// Snapshot of a single controller's input state for one poll cycle.
/// All values are normalised: sticks are in [-1, 1], triggers in [0, 1].
/// </summary>
public sealed class ControllerState
{
    public int PlayerIndex { get; init; }
    public bool IsConnected { get; init; }
    public bool IsWired { get; init; }
    public string ConnectionType { get; init; } = "Offline";
    public bool HasBattery { get; init; }
    public int BatteryPercent { get; init; }
    public string BatteryText { get; init; } = "N/A";

    // --- Buttons (bitfield mirroring XINPUT_GAMEPAD) ---
    public bool A { get; init; }
    public bool B { get; init; }
    public bool X { get; init; }
    public bool Y { get; init; }
    public bool LB { get; init; }
    public bool RB { get; init; }
    public bool LT_Digital { get; init; }   // trigger pressed past threshold
    public bool RT_Digital { get; init; }
    public bool Start { get; init; }
    public bool Back { get; init; }
    public bool LS { get; init; }           // left stick click
    public bool RS { get; init; }           // right stick click
    public bool DPadUp { get; init; }
    public bool DPadDown { get; init; }
    public bool DPadLeft { get; init; }
    public bool DPadRight { get; init; }
    public bool Guide { get; init; }        // Xbox button (may not be readable via XInput)

    // --- Analog ---
    /// <summary>Left trigger, 0–1.</summary>
    public float LeftTrigger { get; init; }
    /// <summary>Right trigger, 0–1.</summary>
    public float RightTrigger { get; init; }

    /// <summary>Left stick X axis, −1 (left) to +1 (right).</summary>
    public float LeftStickX { get; init; }
    /// <summary>Left stick Y axis, −1 (down) to +1 (up).</summary>
    public float LeftStickY { get; init; }

    /// <summary>Right stick X axis, −1 (left) to +1 (right).</summary>
    public float RightStickX { get; init; }
    /// <summary>Right stick Y axis, −1 (down) to +1 (up).</summary>
    public float RightStickY { get; init; }

    /// <summary>Returns an empty / disconnected state for a given player index.</summary>
    public static ControllerState Disconnected(int playerIndex) =>
        new()
        {
            PlayerIndex = playerIndex,
            IsConnected = false,
            ConnectionType = "Offline",
            HasBattery = false,
            BatteryPercent = 0,
            BatteryText = "Disconnected"
        };
}
