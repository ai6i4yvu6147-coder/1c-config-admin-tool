using ConfigAdmin.Application.RemoteSync;
using Xunit;

namespace ConfigAdmin.Tests;

public class ExportDirectoryMonitorTests : IDisposable
{
    private readonly string _tempDir;

    public ExportDirectoryMonitorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"export-monitor-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // best effort cleanup
        }
    }

    [Fact]
    public void Scan_EmptyDirectory_ReturnsZeroStats()
    {
        var startedAt = DateTimeOffset.UtcNow;
        var stats = ExportDirectoryMonitor.Scan(_tempDir, startedAt);

        Assert.Equal(0, stats.FileCount);
        Assert.Equal(0, stats.TotalBytes);
    }

    [Fact]
    public void Scan_WithFiles_ReturnsCountAndSize()
    {
        var subDir = Path.Combine(_tempDir, "Sub");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(_tempDir, "a.xml"), "hello");
        File.WriteAllText(Path.Combine(subDir, "b.xml"), "12345");

        var stats = ExportDirectoryMonitor.Scan(_tempDir, DateTimeOffset.UtcNow);

        Assert.Equal(2, stats.FileCount);
        Assert.True(stats.TotalBytes > 0);
    }

    [Fact]
    public void FormatProgressMessage_IncludesFilesSizeAndElapsed()
    {
        var message = ExportDirectoryMonitor.FormatProgressMessage(
            new ExportDirectoryStats(3841, 156 * 1024 * 1024, TimeSpan.FromMinutes(7)));

        Assert.Contains("3", message);
        Assert.Contains("841", message);
        Assert.Contains("MB", message);
        Assert.Contains("7", message);
        Assert.StartsWith("Выгрузка 1С:", message);
    }

    [Fact]
    public async Task MonitorAsync_ReportsGrowthUntilCancelled()
    {
        var reports = new List<ExportDirectoryStats>();
        var progress = new Progress<ExportDirectoryStats>(reports.Add);

        using var cts = new CancellationTokenSource();
        var monitorTask = ExportDirectoryMonitor.MonitorAsync(
            _tempDir,
            TimeSpan.FromMilliseconds(50),
            progress,
            cts.Token);

        await Task.Delay(120);
        File.WriteAllText(Path.Combine(_tempDir, "new.xml"), new string('x', 100));
        await Task.Delay(120);

        cts.Cancel();
        try
        {
            await monitorTask;
        }
        catch (OperationCanceledException)
        {
            // expected
        }

        Assert.NotEmpty(reports);
        Assert.Equal(0, reports[0].FileCount);
        Assert.True(reports[^1].FileCount >= 1);
        Assert.True(reports[^1].TotalBytes >= 100);
    }
}
