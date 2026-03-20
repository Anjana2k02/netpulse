namespace NetPulse.Tray;

using Hardcodet.Wpf.TaskbarNotification;
using NetPulse.Core.Helpers;
using NetPulse.Core.Services;
using NetPulse.Dashboard;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

/// <summary>
/// Owns the system tray icon lifetime. Updates the tooltip every second with
/// live speed and today's totals. Manages the right-click context menu.
/// </summary>
public sealed class TrayIconManager : IDisposable
{
    private readonly EtwTrackingService _etw;
    private readonly StorageService     _storage;
    private TaskbarIcon?    _icon;
    private DispatcherTimer? _timer;
    private DashboardWindow? _dashboard;

    private long _prevSent;
    private long _prevRecv;

    public TrayIconManager(EtwTrackingService etw, StorageService storage)
    {
        _etw     = etw;
        _storage = storage;
    }

    public void Initialize()
    {
        _icon = new TaskbarIcon
        {
            IconSource     = LoadIcon(),
            ToolTipText    = "NetPulse",
            ContextMenu    = BuildContextMenu(),
            MenuActivation = PopupActivationMode.RightClick,
            NoLeftClickDelay = true
        };

        // Baseline for speed delta
        _prevSent = _etw.TotalBytesSentToday;
        _prevRecv = _etw.TotalBytesRecvToday;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var sent = _etw.TotalBytesSentToday;
        var recv = _etw.TotalBytesRecvToday;

        var sendRate = Math.Max(0, sent - _prevSent);
        var recvRate = Math.Max(0, recv - _prevRecv);
        _prevSent = sent;
        _prevRecv = recv;

        if (_icon is null) return;

        _icon.ToolTipText =
            $"NetPulse\n" +
            $"Today:  {ByteFormatHelper.Format(recv)} ↓   {ByteFormatHelper.Format(sent)} ↑\n" +
            $"Speed:  {ByteFormatHelper.Format(recvRate)}/s ↓   {ByteFormatHelper.Format(sendRate)}/s ↑";
    }

    private ContextMenu BuildContextMenu()
    {
        var menu = new ContextMenu();
        menu.Background = System.Windows.Media.Brushes.White;

        var menuDetails = new MenuItem { Header = "More Details" };
        menuDetails.Click += (_, _) => OpenDashboard();
        menu.Items.Add(menuDetails);

        menu.Items.Add(new Separator());

        var menuAbout = new MenuItem { Header = "About NetPulse" };
        menuAbout.Click += (_, _) => ShowAbout();
        menu.Items.Add(menuAbout);

        menu.Items.Add(new Separator());

        var menuExit = new MenuItem { Header = "Exit" };
        menuExit.Click += (_, _) => Application.Current.Shutdown();
        menu.Items.Add(menuExit);

        return menu;
    }

    private void OpenDashboard()
    {
        if (_dashboard is { IsVisible: true })
        {
            _dashboard.Activate();
            return;
        }
        _dashboard = new DashboardWindow(_etw, _storage);
        _dashboard.Show();
    }

    private static void ShowAbout()
    {
        MessageBox.Show(
            "NetPulse v1.0\n\n" +
            "Per-process network usage monitor.\n\n" +
            $"Data stored in:\n{StorageService.DataFolder}",
            "About NetPulse",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private static System.Windows.Media.ImageSource LoadIcon()
    {
        var uri = new Uri("pack://application:,,,/Assets/tray-icon.ico");
        return new System.Windows.Media.Imaging.BitmapImage(uri);
    }

    public void Dispose()
    {
        _timer?.Stop();
        _icon?.Dispose();
    }
}
