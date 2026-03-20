namespace NetPulse;

using NetPulse.Core.Services;
using NetPulse.Tray;
using System.Threading;
using System.Windows;

public partial class App : Application
{
    private static Mutex? _mutex;

    private EtwTrackingService?  _etw;
    private StorageService?      _storage;
    private TrayIconManager?     _tray;

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

    protected override void OnExit(ExitEventArgs e)
    {
        _storage?.SaveToday();
        _tray?.Dispose();
        _etw?.Dispose();
        _storage?.Dispose();

        try { _mutex?.ReleaseMutex(); } catch { /* already released */ }
        _mutex?.Dispose();

        base.OnExit(e);
    }
}
