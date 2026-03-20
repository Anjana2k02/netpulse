namespace NetPulse.Dashboard;

using NetPulse.Core.Services;
using NetPulse.Dashboard.ViewModels;
using System.Windows;
using System.Windows.Threading;

public partial class DashboardWindow : Window
{
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
        _timer.Stop();
        base.OnClosed(e);
    }
}
