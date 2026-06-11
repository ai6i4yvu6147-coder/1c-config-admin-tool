using System.IO;

namespace ConfigAdmin.Wpf.Services;

public sealed class FileDialogService
{
    public string? PickExecutable(string? initialPath = null)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Выберите 1cv8.exe",
            Filter = "1cv8.exe|1cv8.exe|Исполняемые файлы (*.exe)|*.exe",
            FileName = "1cv8.exe",
            CheckFileExists = true
        };

        if (!string.IsNullOrWhiteSpace(initialPath))
            dialog.InitialDirectory = Path.GetDirectoryName(initialPath);

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickFolder(string? initialPath = null)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Выберите каталог",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true
        };

        if (!string.IsNullOrWhiteSpace(initialPath) && Directory.Exists(initialPath))
            dialog.InitialDirectory = initialPath;

        return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK
            ? dialog.SelectedPath
            : null;
    }
}
