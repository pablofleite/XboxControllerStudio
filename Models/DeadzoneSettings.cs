namespace XboxControllerStudio.Models;

/// <summary>
/// Deadzone configuration for a single controller.
/// Stored per-profile; the input processor reads this before emitting stick values.
/// </summary>
public sealed class DeadzoneSettings
{
    /// <summary>
    /// Radius of the inner dead zone for the left stick, 0–1.
    /// Input magnitude below this value is reported as zero.
    /// </summary>
    public float LeftInnerDeadzone { get; set; } = 0.12f;

    /// <summary>
    /// Radius of the inner dead zone for the right stick, 0–1.
    /// Input magnitude below this value is reported as zero.
    /// </summary>
    public float RightInnerDeadzone { get; set; } = 0.12f;

    /// <summary>
    /// Backward-compatible single deadzone setter. Writing this value applies to both sticks.
    /// </summary>
    public float InnerDeadzone
    {
        get => (LeftInnerDeadzone + RightInnerDeadzone) * 0.5f;
        set
        {
            LeftInnerDeadzone = value;
            RightInnerDeadzone = value;
        }
    }

    /// <summary>
    /// Outer edge: input magnitude above this is clamped to 1.
    /// Allows partial travel controllers to still reach full range.
    /// </summary>
    public float OuterDeadzone { get; set; } = 0.98f;

    /// <summary>
    /// Anti-deadzone (output bias).  
    /// When the stick exits the inner dead zone, the output starts at this value
    /// instead of 0, so there is no "dead" gap in the feel.
    /// 0 = disabled.
    /// </summary>
    public float AntiDeadzone { get; set; } = 0.0f;
}
