namespace NetPulse.Core.Services;

using System.IO;
using NetPulse.Core.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Persists daily network usage to JSON files in %APPDATA%\NetPulse\data\.
/// Auto-saves every 60 seconds and resets the ETW accumulator at midnight.
/// </summary>
public sealed class StorageService : IDisposable
{
    public static readonly string DataFolder =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NetPulse", "data");

    private readonly EtwTrackingService _etw;
    private readonly System.Timers.Timer _autoSaveTimer;
    private readonly System.Timers.Timer _midnightTimer;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public StorageService(EtwTrackingService etw)
    {
        _etw = etw;
        Directory.CreateDirectory(DataFolder);

        _autoSaveTimer = new System.Timers.Timer(60_000) { AutoReset = true };
        _autoSaveTimer.Elapsed += (_, _) => SaveToday();
        _autoSaveTimer.Start();

        _midnightTimer = new System.Timers.Timer(MillisecondsUntilMidnight())
        {
            AutoReset = false
        };
        _midnightTimer.Elapsed += OnMidnight;
        _midnightTimer.Start();
    }

    // ── Startup load ──────────────────────────────────────────────────────

    /// <summary>
    /// Reads today's persisted JSON and seeds the ETW accumulator so a
    /// restart does not lose progress. Call before EtwTrackingService.Start().
    /// </summary>
    public void LoadToday()
    {
        var path = DailyFilePath(DateOnly.FromDateTime(DateTime.Today));
        if (!File.Exists(path)) return;

        try
        {
            var json   = File.ReadAllText(path);
            var record = JsonSerializer.Deserialize<DailyUsageRecord>(json, JsonOptions);
            if (record?.Apps is { Count: > 0 })
                _etw.SeedData(record.Apps);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Storage] LoadToday failed: {ex.Message}");
        }
    }

    // ── Save ──────────────────────────────────────────────────────────────

    public void SaveToday()
    {
        try
        {
            var record = new DailyUsageRecord
            {
                Date = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd"),
                Apps = _etw.GetSnapshot()
                           .ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            };

            var path = DailyFilePath(DateOnly.FromDateTime(DateTime.Today));
            var json = JsonSerializer.Serialize(record, JsonOptions);

            // Atomic write: temp file → rename (prevents corrupt half-writes)
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, path, overwrite: true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Storage] SaveToday failed: {ex.Message}");
        }
    }

    // ── Dashboard data retrieval ──────────────────────────────────────────

    /// <summary>
    /// Returns usage for a specific date. Today's data is pulled from the live
    /// ETW accumulator; past dates are read from disk.
    /// </summary>
    public DailyUsageRecord? LoadDay(DateOnly date)
    {
        if (date == DateOnly.FromDateTime(DateTime.Today))
        {
            return new DailyUsageRecord
            {
                Date = date.ToString("yyyy-MM-dd"),
                Apps = _etw.GetSnapshot()
                           .ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            };
        }

        var path = DailyFilePath(date);
        if (!File.Exists(path)) return null;

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<DailyUsageRecord>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Aggregates all daily records within [from, to] into a single flat dictionary.
    /// Used by the dashboard for monthly and yearly views.
    /// </summary>
    public Dictionary<string, AppNetworkRecord> LoadRange(DateOnly from, DateOnly to)
    {
        var merged = new Dictionary<string, AppNetworkRecord>(StringComparer.OrdinalIgnoreCase);

        for (var d = from; d <= to; d = d.AddDays(1))
        {
            var day = LoadDay(d);
            if (day?.Apps is null) continue;

            foreach (var (key, record) in day.Apps)
            {
                if (!merged.TryGetValue(key, out var agg))
                {
                    agg = new AppNetworkRecord { ProcessName = record.ProcessName };
                    merged[key] = agg;
                }
                agg._bytesSent     += record.BytesSent;
                agg._bytesReceived += record.BytesReceived;
            }
        }

        return merged;
    }

    // ── Midnight reset ────────────────────────────────────────────────────

    private void OnMidnight(object? sender, System.Timers.ElapsedEventArgs e)
    {
        SaveToday();
        _etw.ResetDailyData();

        _midnightTimer.Interval = MillisecondsUntilMidnight();
        _midnightTimer.Start();
    }

    private static double MillisecondsUntilMidnight()
    {
        var now  = DateTime.Now;
        var next = now.Date.AddDays(1);
        return (next - now).TotalMilliseconds;
    }

    private static string DailyFilePath(DateOnly date)
        => Path.Combine(DataFolder, $"{date:yyyy-MM-dd}.json");

    public void Dispose()
    {
        _autoSaveTimer.Stop();
        _autoSaveTimer.Dispose();
        _midnightTimer.Stop();
        _midnightTimer.Dispose();
    }
}
