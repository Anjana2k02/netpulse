namespace NetPulse.Core.Models;

/// <summary>
/// Bytes sent/received by one process name on one calendar day.
/// Used both in-memory (live accumulation) and as the JSON storage unit.
/// Backing fields are updated via Interlocked on the ETW thread; public
/// properties use Interlocked.Read for safe cross-thread access.
/// </summary>
public class AppNetworkRecord
{
    public string ProcessName { get; set; } = string.Empty;

    // Interlocked-safe backing fields — written by ETW thread, read by UI thread
    internal long _bytesSent;
    internal long _bytesReceived;

    public long BytesSent
    {
        get => Interlocked.Read(ref _bytesSent);
        set => Interlocked.Exchange(ref _bytesSent, value);
    }

    public long BytesReceived
    {
        get => Interlocked.Read(ref _bytesReceived);
        set => Interlocked.Exchange(ref _bytesReceived, value);
    }

    public long TotalBytes => BytesSent + BytesReceived;
}
