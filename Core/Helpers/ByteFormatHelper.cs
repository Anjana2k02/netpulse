namespace NetPulse.Core.Helpers;

public static class ByteFormatHelper
{
    public static string Format(long bytes)
    {
        return bytes switch
        {
            < 1_024             => $"{bytes} B",
            < 1_048_576         => $"{bytes / 1_024.0:F1} KB",
            < 1_073_741_824     => $"{bytes / 1_048_576.0:F1} MB",
            _                   => $"{bytes / 1_073_741_824.0:F2} GB"
        };
    }
}
