namespace XboxControllerStudio.Models;

/// <summary>
/// Which Xbox controller button triggers a mapping action.
/// Mirrors the buttons available in XINPUT_GAMEPAD_* constants.
/// </summary>
public enum ControllerButton
{
    None,
    A, B, X, Y,
    LB, RB,
    Start, Back,
    LS, RS,
    DPadUp, DPadDown, DPadLeft, DPadRight,
    LT_Digital, RT_Digital
}

/// <summary>
/// A single button → keyboard key binding.
/// Virtual key codes follow the Win32 VK_ constants used by SendInput.
/// </summary>
public sealed class ButtonMapping
{
    public ControllerButton Button { get; set; }

    /// <summary>Win32 virtual key code to simulate (e.g. 0x20 = Space, 0x57 = W).</summary>
    public ushort VirtualKey { get; set; }

    /// <summary>
    /// True when this mapping targets a mouse button sentinel instead of a keyboard VK.
    /// Sentinels are values >= 0x1000.
    /// </summary>
    public bool IsMouse => VirtualKey >= 0x1000;

    public const ushort MouseLeft = 0x1000;
    public const ushort MouseRight = 0x1001;
    public const ushort MouseMiddle = 0x1002;
    public const ushort MouseX1 = 0x1003;
    public const ushort MouseX2 = 0x1004;

    public string AssignedInput => FormatInputLabel(VirtualKey);

    /// <summary>Human-readable label shown in the UI.</summary>
    public string Label => $"{Button} -> {FormatInputLabel(VirtualKey)}";

    public static string FormatInputLabel(ushort virtualKey)
    {
        return virtualKey switch
        {
            0x00 => "Unassigned",
            MouseLeft => "Mouse Left",
            MouseRight => "Mouse Right",
            MouseMiddle => "Mouse Middle",
            MouseX1 => "Mouse X1",
            MouseX2 => "Mouse X2",
            0x08 => "Backspace",
            0x09 => "Tab",
            0x0D => "Enter",
            0x1B => "Esc",
            0x20 => "Space",
            0x25 => "Left Arrow",
            0x26 => "Up Arrow",
            0x27 => "Right Arrow",
            0x28 => "Down Arrow",
            0x2E => "Delete",
            0x30 => "0",
            0x31 => "1",
            0x32 => "2",
            0x33 => "3",
            0x34 => "4",
            0x35 => "5",
            0x36 => "6",
            0x37 => "7",
            0x38 => "8",
            0x39 => "9",
            0x41 => "A",
            0x42 => "B",
            0x43 => "C",
            0x44 => "D",
            0x45 => "E",
            0x46 => "F",
            0x47 => "G",
            0x48 => "H",
            0x49 => "I",
            0x4A => "J",
            0x4B => "K",
            0x4C => "L",
            0x4D => "M",
            0x4E => "N",
            0x4F => "O",
            0x50 => "P",
            0x51 => "Q",
            0x52 => "R",
            0x53 => "S",
            0x54 => "T",
            0x55 => "U",
            0x56 => "V",
            0x57 => "W",
            0x58 => "X",
            0x59 => "Y",
            0x5A => "Z",
            0x70 => "F1",
            0x71 => "F2",
            0x72 => "F3",
            0x73 => "F4",
            0x74 => "F5",
            0x75 => "F6",
            0x76 => "F7",
            0x77 => "F8",
            0x78 => "F9",
            0x79 => "F10",
            0x7A => "F11",
            0x7B => "F12",
            0xA0 => "Left Shift",
            0xA1 => "Right Shift",
            0xA2 => "Left Ctrl",
            0xA3 => "Right Ctrl",
            0xA4 => "Left Alt",
            0xA5 => "Right Alt",
            _ => $"VK 0x{virtualKey:X2}"
        };
    }
}

/// <summary>
/// A named mapping profile that can be associated with a game executable.
/// </summary>
public sealed class MappingProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Default";
    public bool IsReadOnly { get; set; }

    /// <summary>Process name (without extension) to auto-activate this profile.</summary>
    public string? TargetProcess { get; set; }

    public bool UseRightStickAsMouse { get; set; }
    public float RightStickMouseSensitivity { get; set; } = 14f;

    public List<ButtonMapping> Mappings { get; set; } = new();
    public DeadzoneSettings Deadzone { get; set; } = new();
}
