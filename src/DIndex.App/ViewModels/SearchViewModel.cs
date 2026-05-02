using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DIndex.Core.Application.Interfaces;
using DIndex.Core.Search;

namespace DIndex.App.ViewModels;

public sealed partial class SearchViewModel : BaseViewModel
{
    private readonly IDataEngine _engine;

    [ObservableProperty] private int _searchMode;

    [ObservableProperty] private string _searchId = "";
    [ObservableProperty] private string _rangeFrom = "";
    [ObservableProperty] private string _rangeTo = "";
    [ObservableProperty] private string _keyPrefix = "";

    public ObservableCollection<SearchResult> Results { get; } = new();

    [ObservableProperty] private string _resultTime = "";
    [ObservableProperty] private int _resultCount;
    [ObservableProperty] private bool _hasResults;

    public SearchViewModel(IDataEngine engine) => _engine = engine;

    [RelayCommand]
    private void Search()
    {
        Results.Clear();
        HasResults = false;

        try
        {
            switch (SearchMode)
            {
                case 0:
                    SearchById();
                    break;

                case 1:
                    SearchByRange();
                    break;

                case 2:
                    SearchByKey();
                    break;
            }
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
    }

    private void SearchById()
    {
        if (!long.TryParse(SearchId, out long id))
        {
            SetError("Введіть коректний числовий Id.");
            return;
        }

        if (_engine.TryGetById(id, out var result) && result is not null)
        {
            Results.Add(result);
            ResultTime = $"{_engine.LastSearchMs} мс";
            ResultCount = 1;
            HasResults = true;
        }
        else
        {
            SetStatus("Запис не знайдено.");
        }
    }

    private void SearchByRange()
    {
        if (!long.TryParse(RangeFrom, out long from) || !long.TryParse(RangeTo, out long to))
        {
            SetError("Введіть коректний діапазон Id.");
            return;
        }

        var items = _engine.SearchByRange(from, to);

        foreach (var r in items)
            Results.Add(r);

        ResultTime = $"{_engine.LastSearchMs} мс";
        ResultCount = items.Length;
        HasResults = items.Length > 0;

        if (!HasResults)
            SetStatus("Записів у діапазоні не знайдено.");
    }

    private void SearchByKey()
    {
        if (string.IsNullOrWhiteSpace(KeyPrefix))
        {
            SetError("Введіть префікс ключа для пошуку.");
            return;
        }

        var items = _engine.SearchByKeyPrefix(KeyPrefix.Trim());

        foreach (var r in items)
            Results.Add(r);

        ResultTime = $"{_engine.LastSearchMs} мс";
        ResultCount = items.Length;
        HasResults = items.Length > 0;

        if (!HasResults)
            SetStatus("Записів з таким префіксом не знайдено.");
    }

    [RelayCommand]
    private void DeleteResult(SearchResult result)
    {
        if (_engine.DeleteRecord(result.Id))
        {
            Results.Remove(result);
            ResultCount--;
            SetStatus($"Запис Id={result.Id} видалено.");
        }
    }
}
