namespace ConfigAdmin.Infrastructure.FileSystem;

public sealed class AtomicDirectoryService
{
    public void ReplaceDirectory(string targetPath, string sourcePath)
    {
        if (!Directory.Exists(sourcePath))
            throw new DirectoryNotFoundException($"Временный каталог не найден: {sourcePath}");

        var parent = Path.GetDirectoryName(targetPath)
            ?? throw new InvalidOperationException("Некорректный путь назначения.");

        Directory.CreateDirectory(parent);

        var backupPath = Path.Combine(parent, $"backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}");
        if (Directory.Exists(targetPath))
        {
            if (Directory.Exists(backupPath))
                Directory.Delete(backupPath, recursive: true);
            Directory.Move(targetPath, backupPath);
        }

        try
        {
            Directory.Move(sourcePath, targetPath);
        }
        catch
        {
            if (Directory.Exists(backupPath) && !Directory.Exists(targetPath))
                Directory.Move(backupPath, targetPath);
            throw;
        }

        if (Directory.Exists(backupPath))
        {
            try
            {
                Directory.Delete(backupPath, recursive: true);
            }
            catch
            {
                // backup остаётся для ручного восстановления
            }
        }
    }

    public void SafeDelete(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
    }
}
