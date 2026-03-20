namespace NetPulse;

using NetPulse.Core.Services;
using NetPulse.Tray;
using System.Threading;
using System.Windows;

public partial class App : Application
{
    private static Mutex? _mutex;

    private EtwTrackingService? _etw;
    private StorageService?     _storage;
    private TrayIconManager?    _tray;
    private ThemeService?       _themeService;

    protected override void OnStartup(StartupEventArgs e)
    {
        // ── Single-instance enforcement ──────────────────────────────────
        _mutex = new Mutex(initiallyOwned: true,
                           name: "Global\\NetPulse_SingleInstance_v1",
                           out bool createdNew);

        if (!createdNew)
        {
            _mutex.Dispose();
            Current.Shutdown();
            return;
        }

        GC.KeepAlive(_mutex);
        base.OnStartup(e);

        // ── Theme ─────────────────────────────────────────────────────────
        _themeService = new ThemeService();
        _themeService.ThemeChanged += ApplyTheme;
        ApplyTheme();

        // ── Wire services ─────────────────────────────────────────────────
        _etw     = new EtwTrackingService();
        _storage = new StorageService(_etw);
        _tray    = new TrayIconManager(_etw, _storage);

        // Restore today's persisted data before starting ETW
        _storage.LoadToday();

        // Start kernel ETW capture
        _etw.Start();

        // Show tray icon
        _tray.Initialize();

        // Register run-on-startup
        if (!StartupRegistrar.IsRegistered())
            StartupRegistrar.Register();
    }

    private void ApplyTheme()
    {
        var uri = _themeService!.IsDarkMode
            ? new Uri("pack://application:,,,/Themes/Dark.xaml")
            : new Uri("pack://application:,,,/Themes/Light.xaml");

        Current.Dispatcher.Invoke(() =>
        {
            Current.Resources.MergedDictionaries.Clear();
            Current.Resources.MergedDictionaries.Add(
                new ResourceDictionary { Source = uri });
        });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _storage?.SaveToday();
        _tray?.Dispose();
        _etw?.Dispose();
        _storage?.Dispose();
        _themeService?.Dispose();

        try { _mutex?.ReleaseMutex(); } catch { /* already released */ }
        _mutex?.Dispose();

        base.OnExit(e);
    }
}
