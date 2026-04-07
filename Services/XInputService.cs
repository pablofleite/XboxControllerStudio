using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;
using XboxControllerStudio.Models;

namespace XboxControllerStudio.Services;

/// <summary>
/// Thin wrapper around the XInput P/Invoke layer.
/// Converts raw XINPUT_STATE structs into our ControllerState model.
///
/// XInput constants:
///   XINPUT_GAMEPAD_DPAD_UP      0x0001
///   XINPUT_GAMEPAD_DPAD_DOWN    0x0002
///   XINPUT_GAMEPAD_DPAD_LEFT    0x0004
///   XINPUT_GAMEPAD_DPAD_RIGHT   0x0008
///   XINPUT_GAMEPAD_START        0x0010
///   XINPUT_GAMEPAD_BACK         0x0020
///   XINPUT_GAMEPAD_LEFT_THUMB   0x0040
///   XINPUT_GAMEPAD_RIGHT_THUMB  0x0080
///   XINPUT_GAMEPAD_LEFT_SHOULDER  0x0100
///   XINPUT_GAMEPAD_RIGHT_SHOULDER 0x0200
///   XINPUT_GAMEPAD_A            0x1000
///   XINPUT_GAMEPAD_B            0x2000
///   XINPUT_GAMEPAD_X            0x4000
///   XINPUT_GAMEPAD_Y            0x8000
/// </summary>
public sealed class XInputService
{
    // XInput trigger threshold for treating them as digital presses
    private const byte TriggerThreshold = 30;

    // Maximum raw values for normalisation
    private const float StickMax = 32767f;
    private const float StickMin = 32768f;  // abs value of short.MinValue
    private const float TriggerMax = 255f;
    private static bool _xinput14BatteryAvailable = true;
    private static bool _xinput13BatteryAvailable = true;
    private static readonly TimeSpan BluetoothGattProbeInterval = TimeSpan.FromSeconds(15);
    private static DateTime _lastBluetoothGattProbeUtc = DateTime.MinValue;
    private static int _cachedBluetoothGattPercent = -1;

    [StructLayout(LayoutKind.Sequential)]
    private struct DEVPROPKEY
    {
        public Guid fmtid;
        public uint pid;
    }

    // Shell-exposed Bluetooth battery percentage key used by Windows for BLE devices.
    private static readonly DEVPROPKEY DEVPKEY_Bluetooth_BatteryLevel = new()
    {
        fmtid = new Guid("104EA319-6EE2-4701-BD47-8DDBF425BBE5"),
        pid = 2
    };

    private const int CR_SUCCESS = 0;
    private const int CR_BUFFER_SMALL = 26;

    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_GAMEPAD
    {
        public ushort wButtons;
        public byte bLeftTrigger;
        public byte bRightTrigger;
        public short sThumbLX;
        public short sThumbLY;
        public short sThumbRX;
        public short sThumbRY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_STATE
    {
        public uint dwPacketNumber;
        public XINPUT_GAMEPAD Gamepad;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_BATTERY_INFORMATION
    {
        public byte BatteryType;
        public byte BatteryLevel;
    }

    // ERROR_DEVICE_NOT_CONNECTED = 1167
    [DllImport("xinput1_4.dll")]
    private static extern uint XInputGetState(uint dwUserIndex, ref XINPUT_STATE pState);

    [DllImport("xinput1_4.dll")]
    private static extern uint XInputGetBatteryInformation(
        uint dwUserIndex,
        byte devType,
        ref XINPUT_BATTERY_INFORMATION pBatteryInformation);

    [DllImport("xinput1_3.dll", EntryPoint = "XInputGetBatteryInformation")]
    private static extern uint XInputGetBatteryInformation13(
        uint dwUserIndex,
        byte devType,
        ref XINPUT_BATTERY_INFORMATION pBatteryInformation);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern int CM_Locate_DevNodeW(
        out uint pdnDevInst,
        string pDeviceID,
        uint ulFlags);

    [DllImport("cfgmgr32.dll")]
    private static extern int CM_Get_DevNode_PropertyW(
        uint dnDevInst,
        ref DEVPROPKEY propertyKey,
        out uint propertyType,
        byte[] propertyBuffer,
        ref uint propertyBufferSize,
        uint ulFlags);

    /// <summary>
    /// Reads the current state for <paramref name="playerIndex"/> (0–3).
    /// Returns a disconnected state if the controller is not present.
    /// </summary>
    public ControllerState GetState(int playerIndex)
    {
        var state = new XINPUT_STATE();
        uint result = XInputGetState((uint)playerIndex, ref state);

        if (result != 0)
            return ControllerState.Disconnected(playerIndex);

        var g = state.Gamepad;
        (bool isWired, string connectionType, bool hasBattery, int percent, string text) battery;
        try
        {
            battery = GetBatteryInfo(playerIndex);
        }
        catch
        {
            // Battery probing should never hide an otherwise connected controller.
            battery = (true, "USB", false, 100, "Wired");
        }

        return new ControllerState
        {
            PlayerIndex = playerIndex,
            IsConnected = true,
            IsWired = battery.isWired,
            ConnectionType = battery.connectionType,
            HasBattery = battery.hasBattery,
            BatteryPercent = battery.percent,
            BatteryText = battery.text,

            // Digital buttons - test individual bits
            A = (g.wButtons & 0x1000) != 0,
            B = (g.wButtons & 0x2000) != 0,
            X = (g.wButtons & 0x4000) != 0,
            Y = (g.wButtons & 0x8000) != 0,
            LB = (g.wButtons & 0x0100) != 0,
            RB = (g.wButtons & 0x0200) != 0,
            Start = (g.wButtons & 0x0010) != 0,
            Back = (g.wButtons & 0x0020) != 0,
            LS = (g.wButtons & 0x0040) != 0,
            RS = (g.wButtons & 0x0080) != 0,
            DPadUp = (g.wButtons & 0x0001) != 0,
            DPadDown = (g.wButtons & 0x0002) != 0,
            DPadLeft = (g.wButtons & 0x0004) != 0,
            DPadRight = (g.wButtons & 0x0008) != 0,

            // Triggers as digital (over threshold)
            LT_Digital = g.bLeftTrigger > TriggerThreshold,
            RT_Digital = g.bRightTrigger > TriggerThreshold,

            // Analogue — normalise to [0,1] and [-1,1]
            LeftTrigger = g.bLeftTrigger / TriggerMax,
            RightTrigger = g.bRightTrigger / TriggerMax,

            LeftStickX = g.sThumbLX >= 0
                               ? g.sThumbLX / StickMax
                               : g.sThumbLX / StickMin,
            LeftStickY = g.sThumbLY >= 0
                               ? g.sThumbLY / StickMax
                               : g.sThumbLY / StickMin,
            RightStickX = g.sThumbRX >= 0
                               ? g.sThumbRX / StickMax
                               : g.sThumbRX / StickMin,
            RightStickY = g.sThumbRY >= 0
                               ? g.sThumbRY / StickMax
                               : g.sThumbRY / StickMin,
        };
    }

    private static (bool isWired, string connectionType, bool hasBattery, int percent, string text) GetBatteryInfo(int playerIndex)
    {
        const byte BATTERY_DEVTYPE_GAMEPAD = 0x00;

        const byte BATTERY_TYPE_DISCONNECTED = 0x00;
        const byte BATTERY_TYPE_WIRED = 0x01;
        const byte BATTERY_TYPE_ALKALINE = 0x02;
        const byte BATTERY_TYPE_NIMH = 0x03;
        const byte BATTERY_TYPE_UNKNOWN = 0xFF;

        const byte BATTERY_LEVEL_EMPTY = 0x00;
        const byte BATTERY_LEVEL_LOW = 0x01;
        const byte BATTERY_LEVEL_MEDIUM = 0x02;
        const byte BATTERY_LEVEL_FULL = 0x03;

        if (!TryGetBatteryRaw(playerIndex, BATTERY_DEVTYPE_GAMEPAD, out var info))
        {
            if (TryGetBluetoothBatteryPercent(out int btPercent))
                return (false, "Bluetooth", true, btPercent, $"{btPercent}%");

            // If XInput state is valid but battery metadata is unavailable,
            // default to wired to avoid masking USB controllers as wireless.
            return (true, "USB", false, 100, "Wired");
        }

        if (info.BatteryType == BATTERY_TYPE_WIRED)
            return (true, "USB", false, 100, "Wired");

        int percent = info.BatteryLevel switch
        {
            BATTERY_LEVEL_EMPTY => 0,
            BATTERY_LEVEL_LOW => 25,
            BATTERY_LEVEL_MEDIUM => 60,
            BATTERY_LEVEL_FULL => 100,
            _ => -1
        };

        bool hasLevel = percent >= 0;

        // In practice, some Bluetooth/adapter combinations report UNKNOWN or DISCONNECTED
        // battery type while still providing a valid battery level. We accept that level.
        bool hasBatteryByType = info.BatteryType is BATTERY_TYPE_ALKALINE or BATTERY_TYPE_NIMH;
        // For UNKNOWN/DISCONNECTED types, EMPTY is often just a default fallback value,
        // not a trustworthy battery reading. Trust only LOW/MEDIUM/FULL.
        bool hasBatteryByLevel =
            (info.BatteryType is BATTERY_TYPE_UNKNOWN or BATTERY_TYPE_DISCONNECTED)
            && info.BatteryLevel is BATTERY_LEVEL_LOW or BATTERY_LEVEL_MEDIUM or BATTERY_LEVEL_FULL;

        bool hasBattery = hasBatteryByType || hasBatteryByLevel;
        string connectionType = TryGetBluetoothBatteryPercent(out _) ? "Bluetooth" : "Wireless";

        if (hasBattery)
        {
            int clamped = Math.Max(0, percent);
            return (false, connectionType, true, clamped, $"{clamped}%");
        }

        if (TryGetBluetoothBatteryPercent(out int bluetoothPercent))
            return (false, "Bluetooth", true, bluetoothPercent, $"{bluetoothPercent}%");

        return (false, "Wireless", false, 0, "Battery Unavailable");
    }

    private static bool TryGetBluetoothBatteryPercent(out int percent)
    {
        if (TryGetBluetoothBatteryFromGatt(out percent))
            return true;

        if (TryGetBluetoothBatteryFromPnpProperty(out percent))
            return true;

        if (TryGetBluetoothBatteryFromRegistry(out percent))
            return true;

        percent = 0;
        return false;
    }

    private static bool TryGetBluetoothBatteryFromGatt(out int percent)
    {
        percent = 0;

        // Throttle GATT queries to keep the input polling path lightweight.
        if ((DateTime.UtcNow - _lastBluetoothGattProbeUtc) < BluetoothGattProbeInterval)
        {
            if (_cachedBluetoothGattPercent is >= 0 and <= 100)
            {
                percent = _cachedBluetoothGattPercent;
                return true;
            }

            return false;
        }

        _lastBluetoothGattProbeUtc = DateTime.UtcNow;

        if (!TryProbeBluetoothGattBattery(out int probed))
            return false;

        _cachedBluetoothGattPercent = probed;
        percent = probed;
        return true;
    }

    private static bool TryProbeBluetoothGattBattery(out int percent)
    {
        percent = 0;

        try
        {
            using var bthleRoot = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\BTHLE");
            if (bthleRoot is null)
                return false;

            foreach (var devKeyName in bthleRoot.GetSubKeyNames())
            {
                if (!devKeyName.StartsWith("DEV_", StringComparison.OrdinalIgnoreCase))
                    continue;

                using var devKey = bthleRoot.OpenSubKey(devKeyName);
                if (devKey is null)
                    continue;

                bool isXboxDevice = false;
                foreach (var instanceKeyName in devKey.GetSubKeyNames())
                {
                    using var instanceKey = devKey.OpenSubKey(instanceKeyName);
                    if (instanceKey is null)
                        continue;

                    string friendly = Convert.ToString(instanceKey.GetValue("FriendlyName")) ?? string.Empty;
                    string desc = Convert.ToString(instanceKey.GetValue("DeviceDesc")) ?? string.Empty;
                    if (friendly.Contains("Xbox", StringComparison.OrdinalIgnoreCase)
                        || desc.Contains("Xbox", StringComparison.OrdinalIgnoreCase)
                        || desc.Contains("XINPUT", StringComparison.OrdinalIgnoreCase))
                    {
                        isXboxDevice = true;
                        break;
                    }
                }

                if (!isXboxDevice)
                    continue;

                string macHex = devKeyName[4..]; // DEV_5cba371f29b2 -> 5cba371f29b2
                if (!ulong.TryParse(macHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong btAddress))
                    continue;

                var bleDevice = BluetoothLEDevice.FromBluetoothAddressAsync(btAddress).AsTask().GetAwaiter().GetResult();
                if (bleDevice is null)
                    continue;

                using (bleDevice)
                {
                    var servicesResult = bleDevice.GetGattServicesForUuidAsync(GattServiceUuids.Battery).AsTask().GetAwaiter().GetResult();
                    if (servicesResult.Status != GattCommunicationStatus.Success)
                        continue;

                    foreach (var service in servicesResult.Services)
                    {
                        using (service)
                        {
                            var charsResult = service.GetCharacteristicsForUuidAsync(GattCharacteristicUuids.BatteryLevel).AsTask().GetAwaiter().GetResult();
                            if (charsResult.Status != GattCommunicationStatus.Success)
                                continue;

                            foreach (var characteristic in charsResult.Characteristics)
                            {
                                var readResult = characteristic.ReadValueAsync(BluetoothCacheMode.Uncached).AsTask().GetAwaiter().GetResult();
                                if (readResult.Status != GattCommunicationStatus.Success || readResult.Value is null)
                                    continue;

                                using var reader = DataReader.FromBuffer(readResult.Value);
                                if (reader.UnconsumedBufferLength < 1)
                                    continue;

                                byte level = reader.ReadByte();
                                if (level <= 100)
                                {
                                    percent = level;
                                    return true;
                                }
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            // Ignore and fallback to other sources.
        }

        return false;
    }

    private static bool TryGetBluetoothBatteryFromPnpProperty(out int percent)
    {
        percent = 0;

        try
        {
            using var bthleRoot = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\BTHLE");
            if (bthleRoot is null)
                return false;

            foreach (var devKeyName in bthleRoot.GetSubKeyNames())
            {
                if (!devKeyName.StartsWith("DEV_", StringComparison.OrdinalIgnoreCase))
                    continue;

                using var devKey = bthleRoot.OpenSubKey(devKeyName);
                if (devKey is null)
                    continue;

                foreach (var instanceKeyName in devKey.GetSubKeyNames())
                {
                    using var instanceKey = devKey.OpenSubKey(instanceKeyName);
                    if (instanceKey is null)
                        continue;

                    string friendly = Convert.ToString(instanceKey.GetValue("FriendlyName")) ?? string.Empty;
                    string desc = Convert.ToString(instanceKey.GetValue("DeviceDesc")) ?? string.Empty;

                    bool isXbox = friendly.Contains("Xbox", StringComparison.OrdinalIgnoreCase)
                                  || desc.Contains("Xbox", StringComparison.OrdinalIgnoreCase)
                                  || desc.Contains("XINPUT", StringComparison.OrdinalIgnoreCase);

                    if (!isXbox)
                        continue;

                    string instanceId = $"BTHLE\\{devKeyName}\\{instanceKeyName}";
                    if (TryGetDeviceByteProperty(instanceId, DEVPKEY_Bluetooth_BatteryLevel, out byte value)
                        && value is > 0 and <= 100)
                    {
                        percent = value;
                        return true;
                    }
                }
            }
        }
        catch
        {
            // Best-effort fallback; ignore environment-specific failures.
        }

        return false;
    }

    private static bool TryGetBluetoothBatteryFromRegistry(out int percent)
    {
        percent = 0;

        try
        {
            using var devices = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\BTHPORT\Parameters\Devices");
            if (devices is null)
                return false;

            foreach (var deviceSub in devices.GetSubKeyNames())
            {
                using var key = devices.OpenSubKey(deviceSub);
                if (key is null)
                    continue;

                object? levelObj = key.GetValue("BatteryLevel");
                if (levelObj is null)
                    continue;

                if (TryConvertToInt(levelObj, out int level) && level is > 0 and <= 100)
                {
                    percent = level;
                    return true;
                }
            }
        }
        catch
        {
            // Best-effort fallback; ignore environment-specific failures.
        }

        return false;
    }

    private static bool TryGetDeviceByteProperty(string instanceId, DEVPROPKEY key, out byte value)
    {
        value = 0;

        int locate = CM_Locate_DevNodeW(out uint devInst, instanceId, 0);
        if (locate != CR_SUCCESS)
            return false;

        uint propertyType;
        uint size = 1;
        byte[] buffer = new byte[size];

        int getResult = CM_Get_DevNode_PropertyW(devInst, ref key, out propertyType, buffer, ref size, 0);
        if (getResult == CR_BUFFER_SMALL && size > buffer.Length)
        {
            buffer = new byte[size];
            getResult = CM_Get_DevNode_PropertyW(devInst, ref key, out propertyType, buffer, ref size, 0);
        }

        if (getResult != CR_SUCCESS || size == 0)
            return false;

        value = buffer[0];
        return true;
    }

    private static bool TryConvertToInt(object value, out int result)
    {
        switch (value)
        {
            case byte b:
                result = b;
                return true;
            case short s:
                result = s;
                return true;
            case int i:
                result = i;
                return true;
            case uint u when u <= int.MaxValue:
                result = (int)u;
                return true;
            case long l when l is >= int.MinValue and <= int.MaxValue:
                result = (int)l;
                return true;
            case string str when int.TryParse(str, out int parsed):
                result = parsed;
                return true;
            default:
                result = 0;
                return false;
        }
    }

    private static bool TryGetBatteryRaw(int playerIndex, byte deviceType, out XINPUT_BATTERY_INFORMATION info)
    {
        // Try xinput1_4 first (Win10/11 default), then fallback to xinput1_3 for devices
        // where 1_4 may report unusable battery metadata over Bluetooth/adapter.
        if (TryGetBatteryRaw14(playerIndex, deviceType, out info))
        {
            if (IsBatteryInfoUsable(info))
                return true;

            // Returned but unusable: attempt 1_3 as a compatibility fallback.
            if (TryGetBatteryRaw13(playerIndex, deviceType, out var info13) && IsBatteryInfoUsable(info13))
            {
                info = info13;
                return true;
            }

            return true;
        }

        if (TryGetBatteryRaw13(playerIndex, deviceType, out info))
            return true;

        info = default;
        return false;
    }

    private static bool TryGetBatteryRaw14(int playerIndex, byte deviceType, out XINPUT_BATTERY_INFORMATION info)
    {
        info = default;
        if (!_xinput14BatteryAvailable)
            return false;

        try
        {
            uint result = XInputGetBatteryInformation((uint)playerIndex, deviceType, ref info);
            return result == 0;
        }
        catch (DllNotFoundException)
        {
            _xinput14BatteryAvailable = false;
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            _xinput14BatteryAvailable = false;
            return false;
        }
    }

    private static bool TryGetBatteryRaw13(int playerIndex, byte deviceType, out XINPUT_BATTERY_INFORMATION info)
    {
        info = default;
        if (!_xinput13BatteryAvailable)
            return false;

        try
        {
            uint result = XInputGetBatteryInformation13((uint)playerIndex, deviceType, ref info);
            return result == 0;
        }
        catch (DllNotFoundException)
        {
            _xinput13BatteryAvailable = false;
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            _xinput13BatteryAvailable = false;
            return false;
        }
    }

    private static bool IsBatteryInfoUsable(XINPUT_BATTERY_INFORMATION info)
    {
        const byte BATTERY_TYPE_DISCONNECTED = 0x00;
        const byte BATTERY_TYPE_WIRED = 0x01;
        const byte BATTERY_TYPE_ALKALINE = 0x02;
        const byte BATTERY_TYPE_NIMH = 0x03;
        const byte BATTERY_TYPE_UNKNOWN = 0xFF;

        const byte BATTERY_LEVEL_LOW = 0x01;
        const byte BATTERY_LEVEL_MEDIUM = 0x02;
        const byte BATTERY_LEVEL_FULL = 0x03;

        if (info.BatteryType == BATTERY_TYPE_WIRED)
            return true;

        if (info.BatteryType is BATTERY_TYPE_ALKALINE or BATTERY_TYPE_NIMH)
            return true;

        if (info.BatteryType is BATTERY_TYPE_UNKNOWN or BATTERY_TYPE_DISCONNECTED)
            return info.BatteryLevel is BATTERY_LEVEL_LOW or BATTERY_LEVEL_MEDIUM or BATTERY_LEVEL_FULL;

        return false;
    }
}
