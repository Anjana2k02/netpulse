namespace NetPulse.Dashboard.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetPulse.Core.Helpers;
using NetPulse.Core.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;

public enum FilterTab { Daily, Monthly, Yearly }

public partial class DashboardViewModel : ObservableObject
{
    private readonly EtwTrackingService _etw;
    private readonly StorageService     _storage;

    [ObservableProperty] private FilterTab _selectedTab  = FilterTab.Daily;
    [ObservableProperty] private string _summaryDownload = "0 B";
    [ObservableProperty] private string _summaryUpload   = "0 B";
    [ObservableProperty] private string _summaryTotal    = "0 B";
    [ObservableProperty] private string _currentSpeed    = "0 B/s ↓   0 B/s ↑";
    [ObservableProperty] private string _downSpeed       = "0 B/s";
    [ObservableProperty] private string _upSpeed         = "0 B/s";
    [ObservableProperty] private string _tabLabel        = "Today";

    public bool IsDailyActive   => SelectedTab == FilterTab.Daily;
    public bool IsMonthlyActive => SelectedTab == FilterTab.Monthly;
    public bool IsYearlyActive  => SelectedTab == FilterTab.Yearly;

    public ObservableCollection<AppUsageRowViewModel> AppRows { get; } = new();
    public ICollectionView AppRowsView { get; }

    public DashboardViewModel(EtwTrackingService etw, StorageService storage)
    {
        _etw     = etw;
        _storage = storage;

        AppRowsView = CollectionViewSource.GetDefaultView(AppRows);
        AppRowsView.SortDescriptions.Add(
            new SortDescription(nameof(AppUsageRowViewModel.TotalBytes),
                                ListSortDirection.Descending));

        PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SelectedTab))
                LoadData();
        };

        LoadData();
    }

    partial void OnSelectedTabChanged(FilterTab value)
    {
        OnPropertyChanged(nameof(IsDailyActive));
        OnPropertyChanged(nameof(IsMonthlyActive));
        OnPropertyChanged(nameof(IsYearlyActive));
    }

    // ── Commands ──────────────────────────────────────────────────────────

    [RelayCommand] public void SelectDaily()   => SelectedTab = FilterTab.Daily;
    [RelayCommand] public void SelectMonthly() => SelectedTab = FilterTab.Monthly;
    [RelayCommand] public void SelectYearly()  => SelectedTab = FilterTab.Yearly;
    [RelayCommand] public void Refresh()       => LoadData();

    // ── Data loading ──────────────────────────────────────────────────────

    private void LoadData()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        var (from, to, label) = SelectedTab switch
        {
            FilterTab.Daily   => (today, today, "Today"),
            FilterTab.Monthly => (new DateOnly(today.Year, today.Month, 1), today,
                                  today.ToString("MMMM yyyy")),
            FilterTab.Yearly  => (new DateOnly(today.Year, 1, 1), today,
                                  today.Year.ToString()),
            _                 => (today, today, "Today")
        };

        TabLabel = label;

        var aggregated = _storage.LoadRange(from, to);

        long totalDown = aggregated.Values.Sum(r => r.BytesReceived);
        long totalUp   = aggregated.Values.Sum(r => r.BytesSent);
        long totalAll  = totalDown + totalUp;

        SummaryDownload = ByteFormatHelper.Format(totalDown);
        SummaryUpload   = ByteFormatHelper.Format(totalUp);
        SummaryTotal    = ByteFormatHelper.Format(totalAll);

        // Rebuild DataGrid rows
        AppRows.Clear();
        foreach (var record in aggregated.Values.OrderByDescending(r => r.TotalBytes))
        {
            AppRows.Add(new AppUsageRowViewModel
            {
                ProcessName   = record.ProcessName,
                BytesSent     = record.BytesSent,
                BytesReceived = record.BytesReceived,
                TotalBytes    = record.TotalBytes
            });
        }

        AppRowsView.Refresh();
    }

    /// <summary>Called every second by DashboardWindow to update live speed display.</summary>
    public void RefreshSpeed(long recvRate, long sendRate)
    {
        DownSpeed    = $"{ByteFormatHelper.Format(recvRate)}/s";
        UpSpeed      = $"{ByteFormatHelper.Format(sendRate)}/s";
        CurrentSpeed = $"{DownSpeed} ↓   {UpSpeed} ↑";
    }
}
