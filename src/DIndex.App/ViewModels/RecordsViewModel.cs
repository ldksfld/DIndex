using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DIndex.Core.Application.Interfaces;
using DIndex.Core.Search;

namespace DIndex.App.ViewModels;

public sealed partial class RecordsViewModel : BaseViewModel
{
    private const int PageSize = 50;

    private readonly IDataEngine _engine;

    [ObservableProperty] private string _newId = "";
    [ObservableProperty] private string _newKey = "";
    [ObservableProperty] private string _newData = "";
    [ObservableProperty] private string _validationError = "";

    public bool HasValidationError => !string.IsNullOrEmpty(ValidationError);

    partial void OnValidationErrorChanged(string value) => OnPropertyChanged(nameof(HasValidationError));

    public ObservableCollection<SearchResult> Records { get; } = new();

    [ObservableProperty] private SearchResult? _selectedRecord;
    [ObservableProperty] private string _editData = "";
    [ObservableProperty] private int _totalShown;
    [ObservableProperty] private int _currentPage = 1;
    [ObservableProperty] private int _totalPages = 1;
    [ObservableProperty] private string _pageInfo = "Сторінка 1 / 1";

    public RecordsViewModel(IDataEngine engine) => _engine = engine;

    [RelayCommand]
    private void Add()
    {
        ValidationError = "";

        if (string.IsNullOrWhiteSpace(NewKey))
        {
            ValidationError = "Ключ не може бути порожнім.";
            return;
        }

        if (Encoding.UTF8.GetByteCount(NewKey) > 64)
        {
            ValidationError = "Ключ перевищує 64 байти UTF-8.";
            return;
        }

        if (!string.IsNullOrEmpty(NewData) && Encoding.UTF8.GetByteCount(NewData) > 128)
        {
            ValidationError = "Дані перевищують 128 байт UTF-8.";
            return;
        }

        long? id = null;

        if (!string.IsNullOrWhiteSpace(NewId))
        {
            if (!long.TryParse(NewId, out long parsedId) || parsedId <= 0)
            {
                ValidationError = "Id повинен бути додатнім числом або залишіть порожнім.";
                return;
            }
            id = parsedId;
        }

        try
        {
            long newId = _engine.AddRecord(NewKey, NewData, id);
            SetStatus($"Запис Id={newId} додано успішно.");

            NewId = "";
            NewKey = "";
            NewData = "";

            LoadPageCommand.Execute(null);
        }
        catch (InvalidOperationException ex)
        {
            ValidationError = ex.Message;
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
    }

    [RelayCommand]
    private void DeleteSelected()
    {
        if (SelectedRecord is null)
            return;

        try
        {
            if (_engine.DeleteRecord(SelectedRecord.Id))
            {
                LoadPageCommand.Execute(null);
                SetStatus("Запис видалено.");
            }
        }
        catch (Exception ex)
        {
            SetError($"Не вдалося видалити запис: {ex.Message}");
        }
    }

    [RelayCommand]
    private void UpdateSelected()
    {
        if (SelectedRecord is null || string.IsNullOrWhiteSpace(EditData))
            return;

        if (Encoding.UTF8.GetByteCount(EditData) > 128)
        {
            ValidationError = "Дані перевищують 128 байт UTF-8.";
            return;
        }

        ValidationError = "";

        try
        {
            if (_engine.UpdateRecord(SelectedRecord.Id, EditData))
            {
                LoadPageCommand.Execute(null);
                SetStatus("Запис оновлено.");
            }
        }
        catch (Exception ex)
        {
            SetError($"Не вдалося оновити запис: {ex.Message}");
        }
    }

    partial void OnSelectedRecordChanged(SearchResult? value)
    {
        EditData = value?.Data ?? "";
    }

    [RelayCommand]
    private void ClearAll()
    {
        var confirm = System.Windows.MessageBox.Show(
            "Видалити ВСІ записи? Цю дію неможливо відмінити.",
            "Підтвердження",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (confirm != System.Windows.MessageBoxResult.Yes)
            return;

        try
        {
            _engine.ClearAll();
        }
        catch (Exception ex)
        {
            SetError($"Не вдалося очистити: {ex.Message}");
            return;
        }

        Records.Clear();
        TotalShown = 0;
        CurrentPage = 1;
        TotalPages = 1;
        PageInfo = "Сторінка 1 / 1";
        SelectedRecord = null;

        SetStatus("Всі записи видалено.");
    }

    [RelayCommand]
    private void PreviousPage()
    {
        if (CurrentPage <= 1)
            return;

        CurrentPage--;
        LoadPageCommand.Execute(null);
    }

    [RelayCommand]
    private void NextPage()
    {
        if (CurrentPage >= TotalPages)
            return;

        CurrentPage++;
        LoadPageCommand.Execute(null);
    }

    [RelayCommand]
    public async Task LoadPage()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        ClearError();
        PageInfo = "Завантаження...";

        try
        {
            int total = _engine.ActiveCount;
            int totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));

            if (CurrentPage > totalPages) CurrentPage = totalPages;
            if (CurrentPage < 1) CurrentPage = 1;

            int skip = (CurrentPage - 1) * PageSize;

            var page = await Task.Run(() => _engine.GetActiveRecordsPage(skip, PageSize));

            TotalShown = total;
            TotalPages = totalPages;

            Records.Clear();
            foreach (var r in page)
                Records.Add(r);

            PageInfo = $"Сторінка {CurrentPage} / {TotalPages}";
        }
        catch (InvalidOperationException ex)
        {
            Records.Clear();
            TotalShown = 0;
            TotalPages = 1;
            CurrentPage = 1;
            PageInfo = "Сторінка 1 / 1";
            SetError(ex.Message);
        }
        catch (Exception ex)
        {
            Records.Clear();
            TotalShown = 0;
            PageInfo = "Сторінка 1 / 1";
            SetError($"Не вдалося завантажити записи: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
