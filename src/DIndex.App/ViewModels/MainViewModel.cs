using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DIndex.Core.Application;
using DIndex.Core.Application.Interfaces;

namespace DIndex.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IDataEngine _engine;

    public DashboardViewModel Dashboard { get; }
    public SearchViewModel Search { get; }
    public RecordsViewModel Records { get; }
    public ImportViewModel Import { get; }
    public LogViewModel Log { get; }
    public SnapshotViewModel Snapshot { get; }
    public BenchmarkViewModel Benchmark { get; }

    [ObservableProperty] private BaseViewModel _currentPage;
    [ObservableProperty] private int _navIndex;

    public MainViewModel()
    {
        _engine = new DataEngine();

        Dashboard = new DashboardViewModel(_engine);
        Search = new SearchViewModel(_engine);
        Records = new RecordsViewModel(_engine);
        Import = new ImportViewModel(_engine);
        Log = new LogViewModel(_engine);
        Snapshot = new SnapshotViewModel(_engine);
        Benchmark = new BenchmarkViewModel();

        _currentPage = Dashboard;
        try { Dashboard.RefreshCommand.Execute(null); }
        catch { }
    }

    [RelayCommand]
    private void NavigateDashboard()
    {
        CurrentPage = Dashboard;
        NavIndex = 0;
        try { Dashboard.RefreshCommand.Execute(null); }
        catch { }
    }

    [RelayCommand]
    private void NavigateSearch()
    {
        CurrentPage = Search;
        NavIndex = 1;
    }

    [RelayCommand]
    private void NavigateRecords()
    {
        CurrentPage = Records;
        NavIndex = 2;
        try { Records.LoadPageCommand.Execute(null); }
        catch { }
    }

    [RelayCommand]
    private void NavigateImport()
    {
        CurrentPage = Import;
        NavIndex = 3;
    }

    [RelayCommand]
    private void NavigateLog()
    {
        CurrentPage = Log;
        NavIndex = 4;
        try { Log.RefreshCommand.Execute(null); }
        catch { }
    }

    [RelayCommand]
    private void NavigateSnapshot()
    {
        CurrentPage = Snapshot;
        NavIndex = 5;
    }

    [RelayCommand]
    private void NavigateBenchmark()
    {
        CurrentPage = Benchmark;
        NavIndex = 6;
    }

    public void Dispose()
    {
        Benchmark.Dispose();
        (_engine as IDisposable)?.Dispose();
    }
}
