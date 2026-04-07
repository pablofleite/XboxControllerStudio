using System.Runtime.InteropServices;

namespace XboxControllerStudio.Services;

/// <summary>
/// Sends synthetic keyboard input via the Win32 SendInput API.
/// Only keyboard simulation is implemented for this initial version.
///
/// Security note: SendInput can only target the foreground window;
/// it cannot inject into elevated processes from a non-elevated context.
/// </summary>
public sealed class SendInputService
{
    // INPUT.type values
    private const uint INPUT_KEYBOARD = 1;
    private const uint INPUT_MOUSE = 0;

    // KEYEVENTF flags
    private const uint KEYEVENTF_KEYUP = 0x0002;

    // MOUSEEVENTF flags
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_MOVE = 0x0001;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
    private const uint MOUSEEVENTF_XDOWN = 0x0080;
    private const uint MOUSEEVENTF_XUP = 0x0100;

    private const uint XBUTTON1 = 0x0001;
    private const uint XBUTTON2 = 0x0002;

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct INPUTUNION
    {
        [FieldOffset(0)]
        public KEYBDINPUT ki;

        [FieldOffset(0)]
        public MOUSEINPUT mi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public INPUTUNION U;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    /// <summary>
    /// Sends a key-down event for the given virtual key.
    /// </summary>
    public void KeyDown(ushort virtualKey)
    {
        Send(virtualKey, keyUp: false);
    }

    /// <summary>
    /// Sends a key-up event for the given virtual key.
    /// </summary>
    public void KeyUp(ushort virtualKey)
    {
        Send(virtualKey, keyUp: true);
    }

    public void MouseButtonDown(ushort button)
    {
        SendMouse(button, keyUp: false);
    }

    public void MouseButtonUp(ushort button)
    {
        SendMouse(button, keyUp: true);
    }

    public void MouseMoveRelative(int deltaX, int deltaY)
    {
        if (deltaX == 0 && deltaY == 0)
            return;

        var inputs = new INPUT[]
        {
            new INPUT
            {
                type = INPUT_MOUSE,
                U = new INPUTUNION
                {
                    mi = new MOUSEINPUT
                    {
                        dx = deltaX,
                        dy = deltaY,
                        mouseData = 0,
                        dwFlags = MOUSEEVENTF_MOVE,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            }
        };

        SendInput(1, inputs, Marshal.SizeOf<INPUT>());
    }

    private void Send(ushort virtualKey, bool keyUp)
    {
        var inputs = new INPUT[]
        {
            new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new INPUTUNION
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = virtualKey,
                        dwFlags = keyUp ? KEYEVENTF_KEYUP : 0
                    }
                }
            }
        };

        SendInput(1, inputs, Marshal.SizeOf<INPUT>());
    }

    private void SendMouse(ushort button, bool keyUp)
    {
        uint flags;
        uint mouseData = 0;

        switch (button)
        {
            case Models.ButtonMapping.MouseLeft:
                flags = keyUp ? MOUSEEVENTF_LEFTUP : MOUSEEVENTF_LEFTDOWN;
                break;
            case Models.ButtonMapping.MouseRight:
                flags = keyUp ? MOUSEEVENTF_RIGHTUP : MOUSEEVENTF_RIGHTDOWN;
                break;
            case Models.ButtonMapping.MouseMiddle:
                flags = keyUp ? MOUSEEVENTF_MIDDLEUP : MOUSEEVENTF_MIDDLEDOWN;
                break;
            case Models.ButtonMapping.MouseX1:
                flags = keyUp ? MOUSEEVENTF_XUP : MOUSEEVENTF_XDOWN;
                mouseData = XBUTTON1;
                break;
            case Models.ButtonMapping.MouseX2:
                flags = keyUp ? MOUSEEVENTF_XUP : MOUSEEVENTF_XDOWN;
                mouseData = XBUTTON2;
                break;
            default:
                return;
        }

        var inputs = new INPUT[]
        {
            new INPUT
            {
                type = INPUT_MOUSE,
                U = new INPUTUNION
                {
                    mi = new MOUSEINPUT
                    {
                        dx = 0,
                        dy = 0,
                        mouseData = mouseData,
                        dwFlags = flags,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            }
        };

        SendInput(1, inputs, Marshal.SizeOf<INPUT>());
    }
}
