using Microsoft.Win32;

namespace XboxControllerStudio.Services;

public sealed class StartupRegistrationService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "XboxControllerStudio";

    public bool IsEnabled()
    {
        using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        var value = runKey?.GetValue(ValueName) as string;
        return string.Equals(value, BuildCommand(), StringComparison.Ordinal);
    }

    public void SetEnabled(bool enabled)
    {
        using var runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath);

        if (runKey is null)
            return;

        if (enabled)
            runKey.SetValue(ValueName, BuildCommand(), RegistryValueKind.String);
        else
            runKey.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    private static string BuildCommand()
    {
        string? processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
            throw new InvalidOperationException("Unable to resolve the current process path.");

        return $"\"{processPath}\"";
    }
}