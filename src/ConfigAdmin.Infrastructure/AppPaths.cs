namespace ConfigAdmin.Infrastructure;

public static class AppPaths
{
    public static string AppDataDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ConfigAdmin");

    public static string DatabasePath =>
        Path.Combine(AppDataDirectory, "configadmin.db");

    public static string LogsDirectory =>
        Path.Combine(AppDataDirectory, "logs");

    public static string RunsDirectory =>
        Path.Combine(AppDataDirectory, "runs");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(AppDataDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(RunsDirectory);
    }
}
