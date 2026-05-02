using System.Windows;
using DIndex.App.ViewModels;

namespace DIndex.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var vm = new MainViewModel();
        var win = new MainWindow { DataContext = vm };
        win.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (Current.MainWindow?.DataContext is MainViewModel vm)
            vm.Dispose();

        base.OnExit(e);
    }
}
