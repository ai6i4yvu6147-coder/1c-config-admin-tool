using ConfigAdmin.Integration.OneC;
using Xunit;

namespace ConfigAdmin.Tests;

public sealed class OneCOutLogReaderTests : IDisposable
{
    private readonly string _tempDir;

    public OneCOutLogReaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "configadmin-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void ReadText_ReturnsNull_WhenFileMissing()
    {
        Assert.Null(OneCOutLogReader.ReadText(Path.Combine(_tempDir, "missing.out.log")));
    }

    [Fact]
    public void ReadText_ReadsUtf8Content()
    {
        var path = Path.Combine(_tempDir, "utf8.out.log");
        File.WriteAllText(path, "Ошибка выгрузки конфигурации", System.Text.Encoding.UTF8);

        Assert.Equal("Ошибка выгрузки конфигурации", OneCOutLogReader.ReadText(path));
    }

    [Fact]
    public void ResolveErrorMessage_PrefersOutLogOverExitCode()
    {
        var path = Path.Combine(_tempDir, "error.out.log");
        File.WriteAllText(path, "Не удалось подключиться к информационной базе.");

        var message = OneCOutLogReader.ResolveErrorMessage(
            success: false,
            timedOut: false,
            exitCode: 1,
            standardError: null,
            outLogPath: path);

        Assert.Equal("Не удалось подключиться к информационной базе.", message);
    }

    [Fact]
    public void ResolveErrorMessage_ReturnsTimeoutMessage_WhenTimedOut()
    {
        var message = OneCOutLogReader.ResolveErrorMessage(
            success: false,
            timedOut: true,
            exitCode: -1,
            standardError: "ignored",
            outLogPath: null);

        Assert.Equal("Превышено время ожидания процесса 1С.", message);
    }

    [Fact]
    public void Truncate_AppendsEllipsis_WhenTooLong()
    {
        var text = new string('x', 9000);
        var result = OneCOutLogReader.Truncate(text);

        Assert.NotNull(result);
        Assert.True(result!.Length < text.Length);
        Assert.EndsWith("…", result.TrimEnd());
    }
}
