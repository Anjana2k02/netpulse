namespace NetPulse.Core.Helpers;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

/// <summary>
/// Extracts a process's shell icon from its executable and returns a
/// frozen WPF <see cref="ImageSource"/>. Results are cached by process name.
/// </summary>
public static class ProcessIconHelper
{
    private static readonly ConcurrentDictionary<string, ImageSource?> _cache = new(
        StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Returns the icon for <paramref name="processName"/> asynchronously.
    /// Returns <c>null</c> if the icon cannot be found.
    /// </summary>
    public static Task<ImageSource?> GetIconAsync(string processName) =>
        _cache.TryGetValue(processName, out var cached)
            ? Task.FromResult(cached)
            : Task.Run(() =>
            {
                var icon = LoadIcon(processName);
                _cache[processName] = icon;
                return icon;
            });

    private static ImageSource? LoadIcon(string processName)
    {
        var path = FindExePath(processName);
        if (path == null) return null;

        try
        {
            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
            if (icon == null) return null;

            var bs = Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle, Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            bs.Freeze();
            return bs;
        }
        catch
        {
            return null;
        }
    }

    private static string? FindExePath(string processName)
    {
        // 1. Try a currently-running process
        try
        {
            var proc = Process.GetProcessesByName(processName).FirstOrDefault();
            if (proc != null)
            {
                var path = proc.MainModule?.FileName;
                if (path != null && File.Exists(path)) return path;
            }
        }
        catch { /* access denied on system processes */ }

        // 2. Search well-known directories
        var searchRoots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            Environment.GetFolderPath(Environment.SpecialFolder.SystemX86),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetEnvironmentVariable("WINDIR") ?? string.Empty,
        };

        var exeName = processName + ".exe";
        foreach (var root in searchRoots)
        {
            if (string.IsNullOrEmpty(root)) continue;
            var candidate = Path.Combine(root, exeName);
            if (File.Exists(candidate)) return candidate;
        }

        return null;
    }
}
