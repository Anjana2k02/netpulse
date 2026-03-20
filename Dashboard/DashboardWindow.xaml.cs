namespace NetPulse.Dashboard;

using Microsoft.Win32;
using NetPulse.Core.Services;
using NetPulse.Dashboard.ViewModels;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

public partial class DashboardWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int v, int sz);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    private readonly DashboardViewModel _viewModel;
    private readonly EtwTrackingService _etw;
    private readonly DispatcherTimer    _timer;

    private long _prevSent;
    private long _prevRecv;
    private int  _tickCount;

    public DashboardWindow(EtwTrackingService etw, StorageService storage)
    {
        InitializeComponent();

        _etw       = etw;
        _viewModel = new DashboardViewModel(etw, storage);
        DataContext = _viewModel;

        // Initialise speed baseline to current totals so the first tick shows 0
        _prevSent = etw.TotalBytesSentToday;
        _prevRecv = etw.TotalBytesRecvToday;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    // ── Title bar theming ─────────────────────────────────────────────────

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ApplyTitleBarTheme();
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.General)
            Dispatcher.Invoke(ApplyTitleBarTheme);
    }

    private static bool SystemIsDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return (int)(key?.GetValue("AppsUseLightTheme") ?? 1) == 0;
        }
        catch { return false; }
    }

    private void ApplyTitleBarTheme()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        int v = SystemIsDark() ? 1 : 0;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref v, sizeof(int));
    }

    // ── Timer tick ────────────────────────────────────────────────────────

    private void OnTick(object? sender, EventArgs e)
    {
        var sent = _etw.TotalBytesSentToday;
        var recv = _etw.TotalBytesRecvToday;

        var sendRate = Math.Max(0, sent - _prevSent);
        var recvRate = Math.Max(0, recv - _prevRecv);
        _prevSent = sent;
        _prevRecv = recv;

        _viewModel.RefreshSpeed(recvRate, sendRate);

        // Reload data every 5 seconds to keep totals fresh without jank
        if (++_tickCount % 5 == 0)
            _viewModel.Refresh();
    }

    protected override void OnClosed(EventArgs e)
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        _timer.Stop();
        base.OnClosed(e);
    }
}
