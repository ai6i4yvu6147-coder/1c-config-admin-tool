using System.Globalization;

namespace ConfigAdmin.Application.RemoteSync;

public sealed record ExportDirectoryStats(int FileCount, long TotalBytes, TimeSpan Elapsed);

public static class ExportDirectoryMonitor
{
    public const string ProgressMessagePrefix = "Выгрузка 1С:";

    public static bool IsRoutineStatusMessage(string message) =>
        message.StartsWith(ProgressMessagePrefix, StringComparison.Ordinal) ||
        message.StartsWith("Upload ", StringComparison.OrdinalIgnoreCase);

    public static async Task MonitorAsync(
        string rootPath,
        TimeSpan interval,
        IProgress<ExportDirectoryStats> progress,
        CancellationToken ct = default)
    {
        var startedAt = DateTimeOffset.UtcNow;

        try
        {
            progress.Report(Scan(rootPath, startedAt));

            using var timer = new PeriodicTimer(interval);
            while (await timer.WaitForNextTickAsync(ct))
                progress.Report(Scan(rootPath, startedAt));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // export finished or caller cancelled monitoring
        }
    }

    public static ExportDirectoryStats Scan(string rootPath, DateTimeOffset startedAt)
    {
        if (!Directory.Exists(rootPath))
        {
            return new ExportDirectoryStats(
                0,
                0,
                DateTimeOffset.UtcNow - startedAt);
        }

        var fileCount = 0;
        long totalBytes = 0;
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true
        };

        foreach (var filePath in Directory.EnumerateFiles(rootPath, "*", options))
        {
            try
            {
                totalBytes += new FileInfo(filePath).Length;
                fileCount++;
            }
            catch (IOException)
            {
                // file may be locked while 1C is writing
            }
            catch (UnauthorizedAccessException)
            {
                // skip inaccessible entries
            }
        }

        return new ExportDirectoryStats(
            fileCount,
            totalBytes,
            DateTimeOffset.UtcNow - startedAt);
    }

    public static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(12);

    public static string FormatProgressMessage(ExportDirectoryStats stats)
    {
        var files = stats.FileCount.ToString("N0", CultureInfo.CurrentCulture);
        var size = FormatBytes(stats.TotalBytes);
        var elapsed = FormatElapsed(stats.Elapsed);
        return $"{ProgressMessagePrefix} ~{files} файлов, ~{size}, {elapsed}";
    }

    public static string FormatBytes(long bytes) =>
        bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
            < 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
            _ => $"{bytes / (1024.0 * 1024 * 1024):F1} GB"
        };

    public static string FormatElapsed(TimeSpan elapsed)
    {
        if (elapsed.TotalHours >= 1)
            return $"{(int)elapsed.TotalHours} ч {elapsed.Minutes} мин";

        if (elapsed.TotalMinutes >= 1)
            return $"{(int)elapsed.TotalMinutes} мин";

        return $"{(int)elapsed.TotalSeconds} сек";
    }
}
