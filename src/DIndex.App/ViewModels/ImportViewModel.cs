using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DIndex.Core.Application.Interfaces;
using DIndex.Core.Application.Services;
using Microsoft.Win32;

namespace DIndex.App.ViewModels;

public sealed partial class ImportViewModel : BaseViewModel
{
    private readonly IDataEngine _engine;
    private CancellationTokenSource? _cts;

    [ObservableProperty] private int _generateCount = 10_000;
    [ObservableProperty] private int _progress;
    [ObservableProperty] private string _progressText = "";
    [ObservableProperty] private string _previewText = "";
    [ObservableProperty] private bool _canCancel;
    [ObservableProperty] private string _resultText = "";

    public ImportViewModel(IDataEngine engine) => _engine = engine;

    [RelayCommand]
    private async Task OpenCsvAsync()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "CSV файли (*.csv)|*.csv|Всі файли (*.*)|*.*",
            Title = "Відкрити CSV файл"
        };

        if (dlg.ShowDialog() is not true)
            return;

        var lines = CsvImporter.Preview(dlg.FileName, 10);
        PreviewText = string.Join(Environment.NewLine, lines.Where(l => l is not null));

        await RunAsync(async () =>
        {
            _cts = new CancellationTokenSource();
            CanCancel = true;
            Progress = 0;

            try
            {
                var prog = new Progress<int>(v =>
                {
                    Progress = v;
                    ProgressText = $"Оброблено {v}%";
                });

                var (imported, skipped, cancelled) = await CsvImporter.ImportAsync(_engine, dlg.FileName, prog, _cts.Token);
                ResultText = cancelled
                    ? $"Скасовано. Імпортовано: {imported} | Пропущено: {skipped}"
                    : $"Імпортовано: {imported} | Пропущено: {skipped}";
            }
            finally
            {
                CanCancel = false;
            }
        });
    }

    [RelayCommand]
    private async Task GenerateAsync()
    {
        int count = Math.Max(1000, Math.Min(GenerateCount, 1_000_000));

        await RunAsync(async () =>
        {
            _cts = new CancellationTokenSource();
            CanCancel = true;
            Progress = 0;

            try
            {
                var prog = new Progress<int>(v =>
                {
                    Progress = v;
                    ProgressText = $"Оброблено {v * count / 100} з {count} записів";
                });

                var (generated, cancelled) = await TestDataGenerator.GenerateAsync(_engine, count, prog, _cts.Token);
                ResultText = cancelled
                    ? $"Скасовано. Згенеровано {generated} з {count} записів."
                    : $"Згенеровано {generated} записів успішно.";
            }
            finally
            {
                CanCancel = false;
            }
        });
    }

    [RelayCommand]
    private void Cancel()
    {
        _cts?.Cancel();
        CanCancel = false;
    }
}
