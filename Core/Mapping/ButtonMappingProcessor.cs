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
            bool wasPressed = GetButton(prev, mapping.Button);
            bool isPressed = GetButton(current, mapping.Button);

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

    private static bool GetButton(ControllerState s, ControllerButton btn) => btn switch
    {
        ControllerButton.A => s.A,
        ControllerButton.B => s.B,
        ControllerButton.X => s.X,
        ControllerButton.Y => s.Y,
        ControllerButton.LB => s.LB,
        ControllerButton.RB => s.RB,
        ControllerButton.Start => s.Start,
        ControllerButton.Back => s.Back,
        ControllerButton.LS => s.LS,
        ControllerButton.RS => s.RS,
        ControllerButton.DPadUp => s.DPadUp,
        ControllerButton.DPadDown => s.DPadDown,
        ControllerButton.DPadLeft => s.DPadLeft,
        ControllerButton.DPadRight => s.DPadRight,
        ControllerButton.LT_Digital => s.LT_Digital,
        ControllerButton.RT_Digital => s.RT_Digital,
        _ => false
    };
}
