using CommunityToolkit.Mvvm.ComponentModel;

namespace DIndex.App.ViewModels;

public abstract partial class BaseViewModel : ObservableObject
{
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private string _errorMessage = string.Empty;

    protected void SetError(string msg)
    {
        ErrorMessage = msg;
        HasError = !string.IsNullOrEmpty(msg);
        StatusMessage = msg;
    }

    protected void ClearError()
    {
        ErrorMessage = string.Empty;
        HasError = false;
    }

    protected void SetStatus(string msg)
    {
        StatusMessage = msg;
        ClearError();
    }

    protected async Task RunAsync(Func<Task> action)
    {
        IsBusy = true;
        ClearError();

        try
        {
            await action();
        }
        catch (OperationCanceledException)
        {
            SetStatus("Операцію скасовано.");
        }
        catch (Exception ex)
        {
            SetError($"Помилка: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
