using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ConfigAdmin.Domain.Hub;

namespace ConfigAdmin.Infrastructure.Hub;

public sealed class JsonCliResult
{
    public int ExitCode { get; init; }
    public string StandardOutput { get; init; } = string.Empty;
    public string StandardError { get; init; } = string.Empty;
    public bool Success => ExitCode == 0;
}

public sealed class JsonCliRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<JsonCliResult> RunJsonAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        CancellationToken ct = default) =>
        await RunJsonAsync(executablePath, arguments, environment: null, ct);

    public async Task<JsonCliResult> RunJsonAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string>? environment,
        CancellationToken ct = default)
    {
        if (!File.Exists(executablePath))
            throw new FileNotFoundException($"CLI не найден: {executablePath}");

        var psi = new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        if (environment is not null)
        {
            foreach (var (key, value) in environment)
                psi.Environment[key] = value;
        }

        foreach (var arg in arguments)
            psi.ArgumentList.Add(arg);

        using var process = new Process { StartInfo = psi };
        process.Start();

        await using var stdoutStream = process.StandardOutput.BaseStream;
        await using var stderrStream = process.StandardError.BaseStream;
        using var stdoutBuffer = new MemoryStream();
        using var stderrBuffer = new MemoryStream();
        var copyStdoutTask = stdoutStream.CopyToAsync(stdoutBuffer, ct);
        var copyStderrTask = stderrStream.CopyToAsync(stderrBuffer, ct);
        await Task.WhenAll(copyStdoutTask, copyStderrTask);
        await process.WaitForExitAsync(ct);

        return new JsonCliResult
        {
            ExitCode = process.ExitCode,
            StandardOutput = DecodeProcessOutput(stdoutBuffer.ToArray()),
            StandardError = DecodeProcessOutput(stderrBuffer.ToArray())
        };
    }

    public async Task<T> RunAndDeserializeAsync<T>(
        string executablePath,
        IReadOnlyList<string> arguments,
        CancellationToken ct = default) =>
        await RunAndDeserializeAsync<T>(executablePath, arguments, environment: null, ct);

    public async Task<T> RunAndDeserializeAsync<T>(
        string executablePath,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string>? environment,
        CancellationToken ct = default)
    {
        var result = await RunJsonAsync(executablePath, arguments, environment, ct);
        var json = ExtractJsonPayload(result);
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException(
                $"CLI вернул пустой JSON (exit {result.ExitCode}). {result.StandardError}".Trim());

        return JsonSerializer.Deserialize<T>(json, JsonOptions)
               ?? throw new InvalidOperationException("Не удалось разобрать JSON ответ CLI.");
    }

    public async Task<(T Response, JsonCliResult Raw)> RunAndDeserializeWithRawAsync<T>(
        string executablePath,
        IReadOnlyList<string> arguments,
        CancellationToken ct = default) =>
        await RunAndDeserializeWithRawAsync<T>(executablePath, arguments, environment: null, ct);

    public async Task<(T Response, JsonCliResult Raw)> RunAndDeserializeWithRawAsync<T>(
        string executablePath,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string>? environment,
        CancellationToken ct = default)
    {
        var result = await RunJsonAsync(executablePath, arguments, environment, ct);
        var json = ExtractJsonPayload(result);
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException(
                $"CLI вернул пустой JSON (exit {result.ExitCode}). {result.StandardError}".Trim());

        var parsed = JsonSerializer.Deserialize<T>(json, JsonOptions)
                     ?? throw new InvalidOperationException("Не удалось разобрать JSON ответ CLI.");
        return (parsed, result);
    }

    public async Task<string> WriteTempJsonAsync<T>(T payload, CancellationToken ct = default)
    {
        var path = Path.Combine(Path.GetTempPath(), $"configadmin-{Guid.NewGuid():N}.json");
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), ct);
        return path;
    }

    public static string DecodeProcessOutput(byte[] bytes) =>
        bytes.Length == 0 ? string.Empty : Encoding.UTF8.GetString(bytes);

    private static string ExtractJsonPayload(JsonCliResult result)
    {
        var text = result.StandardOutput.Trim();
        if (string.IsNullOrEmpty(text) && !string.IsNullOrWhiteSpace(result.StandardError))
            text = result.StandardError.Trim();

        var start = text.IndexOf('{');
        if (start < 0)
            return text;

        return text[start..];
    }
}
