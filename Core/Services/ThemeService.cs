namespace NetPulse.Core.Services;

using Microsoft.Win32;

/// <summary>
/// Detects the current Windows app theme (light/dark) and fires
/// <see cref="ThemeChanged"/> whenever the user switches.
/// </summary>
public sealed class ThemeService : IDisposable
{
    private const string ThemeKey =
        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    public bool IsDarkMode { get; private set; }

    /// <summary>Raised on the thread-pool when the system theme changes.</summary>
    public event Action? ThemeChanged;

    public ThemeService()
    {
        IsDarkMode = ReadIsDark();
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    private static bool ReadIsDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(ThemeKey);
            return (int)(key?.GetValue("AppsUseLightTheme") ?? 1) == 0;
        }
        catch
        {
            return false; // default to light on failure
        }
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category != UserPreferenceCategory.General) return;

        var isDark = ReadIsDark();
        if (isDark == IsDarkMode) return;

        IsDarkMode = isDark;
        ThemeChanged?.Invoke();
    }

    public void Dispose() =>
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
}
