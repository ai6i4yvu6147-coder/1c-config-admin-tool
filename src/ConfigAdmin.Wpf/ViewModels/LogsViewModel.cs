using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using ConfigAdmin.Application.Services;
using ConfigAdmin.Wpf.Services;

namespace ConfigAdmin.Wpf.ViewModels;

public sealed class LogsViewModel : ObservableObject
{
    private readonly ExportRunQueryService _exportRunQueryService;
    private readonly INavigationService _navigationService;

    private ExportRunListItem? _selectedRun;
    private ExportRunMetaStep? _selectedStep;
    private string _title = "Журнал выгрузок";
    private string _stepDetailText = string.Empty;

    public LogsViewModel(ExportRunQueryService exportRunQueryService, INavigationService navigationService)
    {
        _exportRunQueryService = exportRunQueryService;
        _navigationService = navigationService;
        Runs = new ObservableCollection<ExportRunListItem>();
        Steps = new ObservableCollection<ExportRunMetaStep>();

        BackCommand = new RelayCommand(() => _navigationService.GoBack());
        RefreshCommand = new RelayCommand(RefreshAsync);
        OpenOutputFolderCommand = new RelayCommand(OpenOutputFolder, () => SelectedRun?.OutputPath is not null);
        OpenRunArtifactsCommand = new RelayCommand(OpenRunArtifacts, () => SelectedRun?.RunArtifactsDirectory is not null);
        OpenMetaCommand = new RelayCommand(OpenMeta, () => SelectedRun?.MetaJsonPath is not null);
        OpenStepOutCommand = new RelayCommand(OpenStepOut, () => SelectedStep?.OutLogPath is not null);
    }

    public ObservableCollection<ExportRunListItem> Runs { get; }
    public ObservableCollection<ExportRunMetaStep> Steps { get; }

    public ExportRunListItem? SelectedRun
    {
        get => _selectedRun;
        set
        {
            if (EqualityComparer<ExportRunListItem?>.Default.Equals(_selectedRun, value))
                return;

            _selectedRun = value;
            RaisePropertyChanged();
            _ = LoadSelectedRunDetailsAsync();
        }
    }

    public ExportRunMetaStep? SelectedStep
    {
        get => _selectedStep;
        set
        {
            if (EqualityComparer<ExportRunMetaStep?>.Default.Equals(_selectedStep, value))
                return;

            _selectedStep = value;
            RaisePropertyChanged();
            UpdateStepDetailText();
        }
    }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string StepDetailText
    {
        get => _stepDetailText;
        set => SetProperty(ref _stepDetailText, value);
    }

    public RelayCommand BackCommand { get; }
    public RelayCommand RefreshCommand { get; }
    public RelayCommand OpenOutputFolderCommand { get; }
    public RelayCommand OpenRunArtifactsCommand { get; }
    public RelayCommand OpenMetaCommand { get; }
    public RelayCommand OpenStepOutCommand { get; }

    private Guid? _filterInfobaseId;

    public void ClearBaseFilter()
    {
        _filterInfobaseId = null;
        Title = "Журнал всех баз";
        _ = RefreshAsync();
    }

    public void SetBaseFilter(Guid infobaseId, string displayName)
    {
        _filterInfobaseId = infobaseId;
        Title = $"Журнал: {displayName}";
        _ = RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        Runs.Clear();
        Steps.Clear();
        SelectedStep = null;
        StepDetailText = string.Empty;

        var runs = await _exportRunQueryService.GetListItemsAsync(_filterInfobaseId);
        foreach (var run in runs)
            Runs.Add(run);
    }

    private async Task LoadSelectedRunDetailsAsync()
    {
        Steps.Clear();
        SelectedStep = null;
        StepDetailText = string.Empty;

        if (SelectedRun is null)
            return;

        var meta = await _exportRunQueryService.LoadMetaAsync(SelectedRun.MetaJsonPath);
        if (meta is null)
        {
            StepDetailText = SelectedRun.ErrorMessage ?? SelectedRun.CommandMasked;
            return;
        }

        foreach (var step in meta.Steps)
            Steps.Add(step);

        var firstFailed = meta.Steps.FirstOrDefault(s => !s.Success);
        if (firstFailed is not null)
            SelectedStep = firstFailed;
        else if (meta.Steps.Count > 0)
            SelectedStep = meta.Steps[0];
    }

    private void UpdateStepDetailText()
    {
        if (SelectedStep is null)
        {
            StepDetailText = string.Empty;
            return;
        }

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(SelectedStep.ErrorMessage))
            parts.Add(SelectedStep.ErrorMessage);
        else if (!string.IsNullOrWhiteSpace(SelectedStep.OutLogExcerpt))
            parts.Add(SelectedStep.OutLogExcerpt);

        if (!string.IsNullOrWhiteSpace(SelectedStep.OutLogPath))
            parts.Add($"Out: {SelectedStep.OutLogPath}");

        StepDetailText = parts.Count > 0
            ? string.Join(Environment.NewLine + Environment.NewLine, parts)
            : "Нет текста ошибки для этого шага.";
    }

    private void OpenOutputFolder()
    {
        var path = SelectedRun?.OutputPath;
        if (string.IsNullOrWhiteSpace(path))
            return;

        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private void OpenRunArtifacts()
    {
        var path = SelectedRun?.RunArtifactsDirectory;
        if (string.IsNullOrWhiteSpace(path))
            return;

        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private void OpenMeta()
    {
        var path = SelectedRun?.MetaJsonPath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;

        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private void OpenStepOut()
    {
        var path = SelectedStep?.OutLogPath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;

        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }
}
