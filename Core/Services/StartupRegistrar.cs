namespace NetPulse.Core.Services;

using Microsoft.Win32;

/// <summary>
/// Registers/unregisters the app in HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run
/// so it launches automatically on Windows login.
/// </summary>
public static class StartupRegistrar
{
    private const string RunKey  = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "NetPulse";

    public static void Register()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            var exePath   = Environment.ProcessPath
                         ?? System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName;
            key?.SetValue(AppName, $"\"{exePath}\"");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Startup] Register failed: {ex.Message}");
        }
    }

    public static void Unregister()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            key?.DeleteValue(AppName, throwOnMissingValue: false);
        }
        catch { /* best-effort */ }
    }

    public static bool IsRegistered()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
            var value     = key?.GetValue(AppName) as string;
            var exePath   = Environment.ProcessPath ?? string.Empty;
            return value is not null &&
                   value.Contains(exePath, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
