using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DIndex.Core.Application.Interfaces;
using DIndex.Core.Storage.Serialization;
using Microsoft.Win32;

namespace DIndex.App.ViewModels;

public sealed partial class SnapshotViewModel : BaseViewModel
{
    private readonly IDataEngine _engine;
    private readonly BinarySnapshotReader _reader = new();
    private CancellationTokenSource? _cts;

    [ObservableProperty] private SnapshotInfo? _info;
    [ObservableProperty] private int _progress;
    [ObservableProperty] private string _resultText = "";
    [ObservableProperty] private int _corruptedCount;
    [ObservableProperty] private bool _hasInfo;

    public SnapshotViewModel(IDataEngine engine) => _engine = engine;

    [RelayCommand]
    private async Task SaveAsync()
    {
        var dlg = new SaveFileDialog
        {
            Filter = "D-Index Snapshot (*.didx)|*.didx",
            DefaultExt = ".didx",
            FileName = $"snapshot_{DateTime.Now:yyyyMMdd_HHmmss}.didx",
            Title = "Зберегти Snapshot"
        };

        if (dlg.ShowDialog() is not true)
            return;

        await RunAsync(async () =>
        {
            _cts = new CancellationTokenSource();
            Progress = 0;

            var prog = new Progress<int>(v => Progress = v);
            bool completed = await Task.Run(() => _engine.SaveSnapshot(dlg.FileName, prog, _cts.Token));

            if (!completed)
            {
                ResultText = "Збереження snapshot скасовано.";
                SetStatus("Збереження скасовано.");
                return;
            }

            Info = _reader.ReadInfo(dlg.FileName);
            HasInfo = true;

            ResultText = $"Snapshot збережено: {dlg.FileName}";
            SetStatus("Snapshot збережено успішно.");
        });
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "D-Index Snapshot (*.didx)|*.didx|Всі файли (*.*)|*.*",
            Title = "Завантажити Snapshot"
        };

        if (dlg.ShowDialog() is not true)
            return;

        await RunAsync(async () =>
        {
            _cts = new CancellationTokenSource();
            Progress = 0;

            var prog = new Progress<int>(v => Progress = v);
            var (info, corrupted) = await Task.Run(
                () => _engine.LoadSnapshot(dlg.FileName, prog, _cts.Token));

            Info = info;
            HasInfo = true;
            CorruptedCount = corrupted;

            ResultText = corrupted > 0
                ? $"Завантажено {info.RecordCount} записів. Пошкоджено: {corrupted}"
                : $"Завантажено {info.RecordCount} записів. Всі записи коректні.";

            SetStatus(ResultText);
        });
    }
}
