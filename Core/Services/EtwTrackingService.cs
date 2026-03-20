namespace NetPulse.Core.Services;

using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;
using NetPulse.Core.Models;
using System.Collections.Concurrent;

/// <summary>
/// Captures per-process network bytes using an ETW kernel session.
/// Runs the blocking ETW processing loop on a dedicated background thread.
/// All byte accumulation uses Interlocked operations for lock-free thread safety.
/// Requires the process to be running as Administrator.
/// </summary>
public sealed class EtwTrackingService : IDisposable
{
    private const string SessionName = "NetPulseEtwSession";

    private TraceEventSession? _session;
    private Thread? _etwThread;

    private readonly ProcessNameResolver _resolver = new();

    // Today's accumulator — atomically replaced at midnight via ResetDailyData()
    private ConcurrentDictionary<string, AppNetworkRecord> _todayData = new();

    // Day-total counters — read by TrayIconManager for speed delta calculation
    private long _totalBytesSentToday;
    private long _totalBytesRecvToday;

    public long TotalBytesSentToday => Interlocked.Read(ref _totalBytesSentToday);
    public long TotalBytesRecvToday => Interlocked.Read(ref _totalBytesRecvToday);

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>Starts the ETW session on a background thread. Call once on startup.</summary>
    public void Start()
    {
        _etwThread = new Thread(RunEtw)
        {
            IsBackground = true,
            Name = "NetPulse-ETW",
            Priority = ThreadPriority.BelowNormal
        };
        _etwThread.Start();
    }

    /// <summary>
    /// Seeds the in-memory accumulator from persisted data (called by StorageService
    /// before Start() on app restart so today's counts resume correctly).
    /// Uses Math.Max to never overwrite a higher live value.
    /// </summary>
    public void SeedData(Dictionary<string, AppNetworkRecord> persisted)
    {
        foreach (var (key, record) in persisted)
        {
            var live = _todayData.GetOrAdd(key, _ => new AppNetworkRecord { ProcessName = record.ProcessName });

            if (record.BytesSent > live.BytesSent)
                Interlocked.Exchange(ref live._bytesSent, record.BytesSent);
            if (record.BytesReceived > live.BytesReceived)
                Interlocked.Exchange(ref live._bytesReceived, record.BytesReceived);
        }

        // Recompute totals from the seeded data
        long sent = _todayData.Values.Sum(r => Interlocked.Read(ref r._bytesSent));
        long recv = _todayData.Values.Sum(r => Interlocked.Read(ref r._bytesReceived));
        Interlocked.Exchange(ref _totalBytesSentToday, sent);
        Interlocked.Exchange(ref _totalBytesRecvToday, recv);
    }

    /// <summary>
    /// Resets all daily counters (called by StorageService at midnight after saving).
    /// </summary>
    public void ResetDailyData()
    {
        Interlocked.Exchange(ref _totalBytesSentToday, 0);
        Interlocked.Exchange(ref _totalBytesRecvToday, 0);
        _todayData = new ConcurrentDictionary<string, AppNetworkRecord>();
        _resolver.ClearCache();
    }

    /// <summary>Thread-safe snapshot of today's per-process data.</summary>
    public IReadOnlyDictionary<string, AppNetworkRecord> GetSnapshot() => _todayData;

    // ── ETW background thread ─────────────────────────────────────────────

    private void RunEtw()
    {
        try
        {
            // Clean up any orphaned session from a previous crash
            TraceEventSession.GetActiveSession(SessionName)?.Stop();

            _session = new TraceEventSession(SessionName)
            {
                StopOnDispose = true
            };

            _session.EnableKernelProvider(KernelTraceEventParser.Keywords.NetworkTCPIP);

            // TCP IPv4
            _session.Source.Kernel.TcpIpSend += data =>
                OnBytes(data.ProcessID, data.size, isSend: true);
            _session.Source.Kernel.TcpIpRecv += data =>
                OnBytes(data.ProcessID, data.size, isSend: false);

            // TCP IPv6
            _session.Source.Kernel.TcpIpSendIPV6 += data =>
                OnBytes(data.ProcessID, data.size, isSend: true);
            _session.Source.Kernel.TcpIpRecvIPV6 += data =>
                OnBytes(data.ProcessID, data.size, isSend: false);

            // UDP IPv4
            _session.Source.Kernel.UdpIpSend += data =>
                OnBytes(data.ProcessID, data.size, isSend: true);
            _session.Source.Kernel.UdpIpRecv += data =>
                OnBytes(data.ProcessID, data.size, isSend: false);

            // Blocks until _session.Stop() or Dispose()
            _session.Source.Process();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ETW] Session error: {ex.Message}");
        }
    }

    private void OnBytes(int pid, int sizeBytes, bool isSend)
    {
        if (sizeBytes <= 0) return;

        var name = _resolver.Resolve(pid);
        var record = _todayData.GetOrAdd(name, _ => new AppNetworkRecord { ProcessName = name });

        if (isSend)
        {
            Interlocked.Add(ref record._bytesSent, sizeBytes);
            Interlocked.Add(ref _totalBytesSentToday, sizeBytes);
        }
        else
        {
            Interlocked.Add(ref record._bytesReceived, sizeBytes);
            Interlocked.Add(ref _totalBytesRecvToday, sizeBytes);
        }
    }

    public void Dispose()
    {
        _session?.Stop();
        _session?.Dispose();
    }
}
