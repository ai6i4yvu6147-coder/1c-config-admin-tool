namespace ConfigAdmin.Wpf.Services;

public static class UiStatusReporter
{
    public static void Report(
        UiActivityLog log,
        string source,
        string statusMessage,
        Action<string> setStatusMessage,
        bool isError,
        string? detail = null)
    {
        setStatusMessage(statusMessage);
        if (isError)
            log.LogError(source, statusMessage, detail);
        else
            log.LogInfo(source, statusMessage, detail);
    }

    public static void ReportException(
        UiActivityLog log,
        string source,
        Exception ex,
        Action<string> setStatusMessage)
    {
        var detail = ex.StackTrace;
        Report(log, source, ex.Message, setStatusMessage, isError: true, detail);
    }
}
