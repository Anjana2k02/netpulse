namespace NetPulse.Dashboard.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using NetPulse.Core.Helpers;

/// <summary>One row in the dashboard's per-process DataGrid.</summary>
public partial class AppUsageRowViewModel : ObservableObject
{
    [ObservableProperty] private string _processName = string.Empty;
    [ObservableProperty] private long   _bytesSent;
    [ObservableProperty] private long   _bytesReceived;
    [ObservableProperty] private long   _totalBytes;

    public string FormattedSent     => ByteFormatHelper.Format(BytesSent);
    public string FormattedReceived => ByteFormatHelper.Format(BytesReceived);
    public string FormattedTotal    => ByteFormatHelper.Format(TotalBytes);

    partial void OnBytesSentChanged(long value)     => OnPropertyChanged(nameof(FormattedSent));
    partial void OnBytesReceivedChanged(long value) => OnPropertyChanged(nameof(FormattedReceived));
    partial void OnTotalBytesChanged(long value)    => OnPropertyChanged(nameof(FormattedTotal));
}
