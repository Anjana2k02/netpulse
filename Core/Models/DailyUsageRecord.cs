namespace NetPulse.Core.Models;

/// <summary>
/// Root of each daily JSON file stored in %APPDATA%\NetPulse\data\YYYY-MM-DD.json.
/// </summary>
public class DailyUsageRecord
{
    /// <summary>ISO date string "yyyy-MM-dd" — matches the file name.</summary>
    public string Date { get; set; } = string.Empty;

    /// <summary>Key = process name (lowercase). Value = accumulated byte counts.</summary>
    public Dictionary<string, AppNetworkRecord> Apps { get; set; } = new();
}
