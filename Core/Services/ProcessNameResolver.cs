namespace NetPulse.Core.Services;

/// <summary>
/// Resolves process IDs to process names with a short-lived cache to handle
/// high-frequency ETW callbacks without hammering the OS process table.
/// </summary>
public sealed class ProcessNameResolver
{
    private readonly Dictionary<int, (string Name, DateTime Expiry)> _cache = new();
    private readonly TimeSpan _cacheTtl = TimeSpan.FromSeconds(30);
    private readonly object _lock = new();

    /// <summary>
    /// Returns the process name for the given PID. Returns "system" for PID 0/4,
    /// and "unknown" if the process cannot be found. Thread-safe.
    /// </summary>
    public string Resolve(int pid)
    {
        if (pid == 0) return "idle";
        if (pid == 4) return "system";

        lock (_lock)
        {
            if (_cache.TryGetValue(pid, out var entry) && entry.Expiry > DateTime.UtcNow)
                return entry.Name;

            try
            {
                var proc = System.Diagnostics.Process.GetProcessById(pid);
                var name = proc.ProcessName.ToLowerInvariant();
                _cache[pid] = (name, DateTime.UtcNow.Add(_cacheTtl));
                return name;
            }
            catch
            {
                // Process already exited or access denied — cache briefly to avoid retry spam
                _cache[pid] = ("unknown", DateTime.UtcNow.AddSeconds(5));
                return "unknown";
            }
        }
    }

    public void ClearCache()
    {
        lock (_lock) { _cache.Clear(); }
    }
}
