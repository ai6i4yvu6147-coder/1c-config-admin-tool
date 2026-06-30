using System.Collections.ObjectModel;
using System.Windows.Threading;
using Serilog;

namespace ConfigAdmin.Wpf.Services;

/// <summary>
/// In-memory журнал UI-событий (ошибки экранов, MCP sync и т.д.) + запись в Serilog-файл.
/// Полноценный единый event bus — см. docs/todo.md §7.
/// </summary>
public sealed class UiActivityLog
{
    private const int MaxEntries = 500;

    public ObservableCollection<UiActivityEntry> Entries { get; } = [];

    public void LogInfo(string source, string message, string? detail = null) =>
        Add(UiActivityLevel.Info, source, message, detail);

    public void LogWarning(string source, string message, string? detail = null) =>
        Add(UiActivityLevel.Warning, source, message, detail);

    public void LogError(string source, string message, string? detail = null) =>
        Add(UiActivityLevel.Error, source, message, detail);

    private void Add(UiActivityLevel level, string source, string message, string? detail)
    {
        var entry = new UiActivityEntry
        {
            Timestamp = DateTimeOffset.Now,
            Level = level,
            Source = source,
            Message = message,
            Detail = detail
        };

        var logLine = string.IsNullOrWhiteSpace(detail) ? message : $"{message} | {detail}";
        switch (level)
        {
            case UiActivityLevel.Error:
                Log.Error("[{Source}] {Message}", source, logLine);
                break;
            case UiActivityLevel.Warning:
                Log.Warning("[{Source}] {Message}", source, logLine);
                break;
            default:
                Log.Information("[{Source}] {Message}", source, logLine);
                break;
        }

        void Insert()
        {
            Entries.Insert(0, entry);
            while (Entries.Count > MaxEntries)
                Entries.RemoveAt(Entries.Count - 1);
        }

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
            Insert();
        else
            dispatcher.Invoke(Insert);
    }
}
