using System.Text;

namespace ConfigAdmin.Integration.OneC;

public static class OneCOutLogReader
{
    private const int MaxExcerptLength = 8000;

    static OneCOutLogReader()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static string? ReadText(string? outLogPath)
    {
        if (string.IsNullOrWhiteSpace(outLogPath) || !File.Exists(outLogPath))
            return null;

        foreach (var encoding in GetEncodings())
        {
            try
            {
                var text = File.ReadAllText(outLogPath, encoding).Trim();
                if (!string.IsNullOrWhiteSpace(text))
                    return text;
            }
            catch
            {
                // try next encoding
            }
        }

        return null;
    }

    private static IEnumerable<Encoding> GetEncodings()
    {
        var encodings = new List<Encoding> { Encoding.UTF8 };

        try
        {
            encodings.Add(Encoding.GetEncoding(1251));
        }
        catch (NotSupportedException)
        {
            // cp1251 unavailable on some runtimes
        }

        return encodings;
    }

    public static string? ResolveErrorMessage(
        bool success,
        bool timedOut,
        int exitCode,
        string? standardError,
        string? outLogPath)
    {
        if (success)
            return null;

        if (timedOut)
            return "Превышено время ожидания процесса 1С.";

        var outText = ReadText(outLogPath);
        var stderr = string.IsNullOrWhiteSpace(standardError) ? null : standardError.Trim();

        if (!string.IsNullOrWhiteSpace(outText) && !string.IsNullOrWhiteSpace(stderr))
            return $"{outText}{Environment.NewLine}{stderr}";

        if (!string.IsNullOrWhiteSpace(outText))
            return Truncate(outText);

        if (!string.IsNullOrWhiteSpace(stderr))
            return stderr;

        return $"Процесс 1С завершился с кодом {exitCode}.";
    }

    public static string? Truncate(string? text) =>
        string.IsNullOrEmpty(text) || text.Length <= MaxExcerptLength
            ? text
            : text[..MaxExcerptLength] + Environment.NewLine + "…";
}
