using ConfigAdmin.Domain.Integration;

namespace ConfigAdmin.Integration.OneC;

public static class DumpResultReader
{
    public static int ResolveExitCode(ProcessResult result, string? dumpResultPath)
    {
        if (!string.IsNullOrWhiteSpace(dumpResultPath) && File.Exists(dumpResultPath))
        {
            var text = File.ReadAllText(dumpResultPath).Trim();
            if (int.TryParse(text, out var code))
                return code;
        }

        return result.ExitCode;
    }
}
