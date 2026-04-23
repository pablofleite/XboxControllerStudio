using XboxControllerStudio.Models;
using XboxControllerStudio.Services;

namespace XboxControllerStudio.Core.Mapping;

/// <summary>
/// Compares consecutive ControllerState snapshots and fires SendInput
/// key events when a mapped button transitions pressed/released.
///
/// Only button edge transitions (down and up) generate events, not
/// continuous hold — preventing SendInput key-repeat spam.
/// </summary>
public sealed class ButtonMappingProcessor
{
    private const float StickPressThreshold = 0.5f;
    private const float StickReleaseThreshold = 0.35f;

    private readonly SendInputService _sendInput;
    private ControllerState? _previous;

    public ButtonMappingProcessor(SendInputService sendInput)
    {
        _sendInput = sendInput;
    }

    /// <summary>
    /// Call this on every poll cycle with the current state and the
    /// active profile.  Sends key events for any changed mappings.
    /// </summary>
    public void Process(ControllerState current, MappingProfile profile)
    {
        var prev = _previous ?? ControllerState.Disconnected(current.PlayerIndex);

        foreach (var mapping in profile.Mappings)
        {
            bool wasPressed = GetButton(prev, prev, mapping.Button);
            bool isPressed = GetButton(current, prev, mapping.Button);

            if (!wasPressed && isPressed)
                SendDown(mapping.VirtualKey);
            else if (wasPressed && !isPressed)
                SendUp(mapping.VirtualKey);
        }

        _previous = current;
    }

    /// <summary>
    /// Resets edge-detection state.  Call when the profile changes or
    /// the controller disconnects to avoid stuck keys.
    /// </summary>
    public void Reset() => _previous = null;

    private void SendDown(ushort virtualKey)
    {
        if (virtualKey == 0)
            return;

        if (virtualKey >= ButtonMapping.MouseLeft)
            _sendInput.MouseButtonDown(virtualKey);
        else
            _sendInput.KeyDown(virtualKey);
    }

    private void SendUp(ushort virtualKey)
    {
        if (virtualKey == 0)
            return;

        if (virtualKey >= ButtonMapping.MouseLeft)
            _sendInput.MouseButtonUp(virtualKey);
        else
            _sendInput.KeyUp(virtualKey);
    }

    private static bool GetButton(ControllerState current, ControllerState previous, ControllerButton btn) => btn switch
    {
        ControllerButton.A => current.A,
        ControllerButton.B => current.B,
        ControllerButton.X => current.X,
        ControllerButton.Y => current.Y,
        ControllerButton.LB => current.LB,
        ControllerButton.RB => current.RB,
        ControllerButton.Start => current.Start,
        ControllerButton.Back => current.Back,
        ControllerButton.LS => current.LS,
        ControllerButton.RS => current.RS,
        ControllerButton.LeftStickUp => GetStickDirection(current.LeftStickY, previous.LeftStickY > 0f),
        ControllerButton.LeftStickDown => GetStickDirection(-current.LeftStickY, previous.LeftStickY < 0f),
        ControllerButton.LeftStickLeft => GetStickDirection(-current.LeftStickX, previous.LeftStickX < 0f),
        ControllerButton.LeftStickRight => GetStickDirection(current.LeftStickX, previous.LeftStickX > 0f),
        ControllerButton.DPadUp => current.DPadUp,
        ControllerButton.DPadDown => current.DPadDown,
        ControllerButton.DPadLeft => current.DPadLeft,
        ControllerButton.DPadRight => current.DPadRight,
        ControllerButton.LT_Digital => current.LT_Digital,
        ControllerButton.RT_Digital => current.RT_Digital,
        _ => false
    };

    private static bool GetStickDirection(float axisValue, bool wasPressed)
    {
        float threshold = wasPressed ? StickReleaseThreshold : StickPressThreshold;
        return axisValue >= threshold;
    }
}
