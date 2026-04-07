using XboxControllerStudio.Models;

namespace XboxControllerStudio.Core.Input;

/// <summary>
/// Applies a radial deadzone to a 2-axis stick vector.
///
/// Radial deadzone treats both axes as a single magnitude, which
/// produces a circular dead zone rather than the square zone you get
/// when clamping each axis independently.  The result is smoother
/// diagonal movement and more accurate centre-snap.
///
/// Algorithm:
///   1. Compute magnitude of (x, y).
///   2. If magnitude &lt; innerDead  → output (0, 0).
///   3. If magnitude &gt; outerEdge  → clamp to unit vector * 1.
///   4. Otherwise rescale linearly from [inner, outer] → [antiDead, 1].
/// </summary>
public static class DeadzoneProcessor
{
    private const float MinRange = 0.0001f;

    /// <summary>
    /// Applies deadzone and returns the processed (x, y) pair.
    /// Input values should be in [-1, 1].
    /// </summary>
    public static (float x, float y) Apply(float rawX, float rawY, DeadzoneSettings settings)
        => Apply(rawX, rawY, settings.InnerDeadzone, settings.OuterDeadzone, settings.AntiDeadzone);

    /// <summary>
    /// Applies radial deadzone for a specific stick.
    /// </summary>
    public static (float x, float y) Apply(float rawX, float rawY, float innerDeadzone, float outerDeadzone, float antiDeadzone)
    {
        float inner = Math.Clamp(innerDeadzone, 0f, 0.95f);
        float outer = Math.Clamp(outerDeadzone, inner + 0.01f, 1f);
        float anti = Math.Clamp(antiDeadzone, 0f, 1f);

        float magnitude = MathF.Sqrt(rawX * rawX + rawY * rawY);

        if (magnitude <= MinRange)
            return (0f, 0f);

        if (magnitude < inner)
            return (0f, 0f);

        if (magnitude > outer)
        {
            // Clamp: preserve direction, force magnitude to 1
            float scale = 1f / magnitude;
            return (rawX * scale, rawY * scale);
        }

        // Linear remap: map [inner, outer] → [antiDead, 1]
        float normalised = (magnitude - inner) / MathF.Max(MinRange, (outer - inner));

        float output = anti + normalised * (1f - anti);

        float dirScale = output / magnitude;
        return (rawX * dirScale, rawY * dirScale);
    }

    /// <summary>
    /// Convenience overload — applies deadzone to both sticks in one call.
    /// Returns a new ControllerState with processed stick values.
    /// </summary>
    public static ControllerState Apply(ControllerState raw, DeadzoneSettings settings)
    {
        var (lx, ly) = Apply(raw.LeftStickX, raw.LeftStickY, settings.LeftInnerDeadzone, settings.OuterDeadzone, settings.AntiDeadzone);
        var (rx, ry) = Apply(raw.RightStickX, raw.RightStickY, settings.RightInnerDeadzone, settings.OuterDeadzone, settings.AntiDeadzone);

        // ControllerState is a record-like immutable init-only object,
        // so we copy it with the processed stick values.
        return new ControllerState
        {
            PlayerIndex = raw.PlayerIndex,
            IsConnected = raw.IsConnected,
            IsWired = raw.IsWired,
            ConnectionType = raw.ConnectionType,
            HasBattery = raw.HasBattery,
            BatteryPercent = raw.BatteryPercent,
            BatteryText = raw.BatteryText,
            A = raw.A,
            B = raw.B,
            X = raw.X,
            Y = raw.Y,
            LB = raw.LB,
            RB = raw.RB,
            LT_Digital = raw.LT_Digital,
            RT_Digital = raw.RT_Digital,
            Start = raw.Start,
            Back = raw.Back,
            LS = raw.LS,
            RS = raw.RS,
            DPadUp = raw.DPadUp,
            DPadDown = raw.DPadDown,
            DPadLeft = raw.DPadLeft,
            DPadRight = raw.DPadRight,
            Guide = raw.Guide,
            LeftTrigger = raw.LeftTrigger,
            RightTrigger = raw.RightTrigger,
            LeftStickX = lx,
            LeftStickY = ly,
            RightStickX = rx,
            RightStickY = ry,
        };
    }
}
