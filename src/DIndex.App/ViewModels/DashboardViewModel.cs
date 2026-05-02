using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DIndex.Core.Application.Interfaces;
using DIndex.Core.Indexing.Bst;

namespace DIndex.App.ViewModels;

public sealed record ChartBar(double BarHeight, string Label, int Count);

public sealed partial class DashboardViewModel : BaseViewModel
{
    private const int MaxRecordsCapacity = 256 * 16_384;

    private readonly IDataEngine _engine;

    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private int _activeCount;
    [ObservableProperty] private int _deletedCount;
    [ObservableProperty] private string _indexDepthDisplay = "0";
    [ObservableProperty] private double _indexSizeMb;
    [ObservableProperty] private long _lastSearchMs;
    [ObservableProperty] private int _pageCount;
    [ObservableProperty] private double _fillPercent;
    [ObservableProperty] private int _logCount;
    [ObservableProperty] private bool _isIndexDeep;
    [ObservableProperty] private bool _hasChartData;

    public ObservableCollection<ChartBar> ChartBars { get; } = new();

    public DashboardViewModel(IDataEngine engine) => _engine = engine;

    [RelayCommand]
    public async Task Refresh()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        ClearError();

        try
        {
            TotalCount = _engine.TotalCount;
            ActiveCount = _engine.ActiveCount;
            DeletedCount = Math.Max(0, TotalCount - ActiveCount);
            PageCount = _engine.PageCount;
            LastSearchMs = _engine.LastSearchMs;
            LogCount = _engine.LogCount;
            IndexSizeMb = Math.Round(TotalCount * 216.0 / (1024 * 1024), 2);
            FillPercent = Math.Round(TotalCount * 100.0 / MaxRecordsCapacity, 2);

            var (depth, series) = await Task.Run(() =>
            {
                int d = _engine.BstHeight;
                var s = _engine.GetTimeSeries(12);
                return (d, s);
            });

            IsIndexDeep = depth == int.MaxValue || depth > BinarySearchTree.DepthWarningThreshold;
            IndexDepthDisplay = depth == int.MaxValue ? "глибокий" : depth.ToString();

            ChartBars.Clear();

            if (series.Length > 0)
            {
                int maxCount = 0;
                foreach (var (_, cnt) in series)
                {
                    if (cnt > maxCount)
                        maxCount = cnt;
                }

                const double MaxBarHeight = 90.0;

                foreach (var (ts, cnt) in series)
                {
                    double h = maxCount > 0 ? cnt * MaxBarHeight / maxCount : 2.0;
                    string lbl = DateTimeOffset.FromUnixTimeSeconds(ts).ToLocalTime().ToString("HH:mm");
                    ChartBars.Add(new ChartBar(h, lbl, cnt));
                }
            }

            HasChartData = ChartBars.Count > 0;
        }
        catch (Exception ex)
        {
            SetError($"Не вдалося оновити метрики: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
