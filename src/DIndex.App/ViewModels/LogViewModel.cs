using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DIndex.Core.Application.Interfaces;
using DIndex.Core.Indexing.LinkedList;

namespace DIndex.App.ViewModels;

public sealed partial class LogViewModel : BaseViewModel
{
    private readonly IDataEngine _engine;

    public ObservableCollection<TransactionEntry> Entries { get; } = new();

    [ObservableProperty] private int _filterIndex;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UndoCommand))]
    private bool _canUndo;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RedoCommand))]
    private bool _canRedo;

    public LogViewModel(IDataEngine engine) => _engine = engine;

    [RelayCommand]
    public void Refresh()
    {
        Entries.Clear();
        var all = _engine.GetTransactionLog();

        var filtered = FilterIndex switch
        {
            1 => all.Where(e => e.Type == OperationType.Insert),
            2 => all.Where(e => e.Type == OperationType.Delete),
            3 => all.Where(e => e.Type == OperationType.Update),
            _ => all.AsEnumerable()
        };

        foreach (var e in filtered)
            Entries.Add(e);

        CanUndo = _engine.CanUndo;
        CanRedo = _engine.CanRedo;
    }

    partial void OnFilterIndexChanged(int value) => Refresh();

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        try
        {
            var entry = _engine.Undo();

            if (entry is not null)
                SetStatus($"Відмінено: {entry.Description}");
        }
        catch (Exception ex)
        {
            SetError($"Не вдалося відмінити: {ex.Message}");
        }

        Refresh();
    }

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo()
    {
        try
        {
            var entry = _engine.Redo();

            if (entry is not null)
                SetStatus($"Повторено: {entry.Description}");
        }
        catch (Exception ex)
        {
            SetError($"Не вдалося повторити: {ex.Message}");
        }

        Refresh();
    }

    [RelayCommand]
    private void ClearLog()
    {
        var result = System.Windows.MessageBox.Show(
            "Ви впевнені, що хочете очистити журнал?",
            "Підтвердження",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (result == System.Windows.MessageBoxResult.Yes)
        {
            _engine.ClearTransactionLog();
            Refresh();
            SetStatus("Журнал очищено.");
        }
    }
}
