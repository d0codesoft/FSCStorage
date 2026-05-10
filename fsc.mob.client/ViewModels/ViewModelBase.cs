using CommunityToolkit.Mvvm.ComponentModel;
using fsc.mob.client.Api;

namespace fsc.mob.client.ViewModels;

public abstract partial class ViewModelBase : ObservableObject
{
    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isUnauthorized;

    protected void ClearMessages()
    {
        ErrorMessage = null;
        StatusMessage = null;
        IsUnauthorized = false;
    }

    protected void SetError(Exception exception)
    {
        if (exception is AdminApiException apiException)
        {
            ErrorMessage = apiException.Message;
            IsUnauthorized = apiException.IsUnauthorized || apiException.IsForbidden;
            return;
        }

        ErrorMessage = exception.Message;
        IsUnauthorized = false;
    }
}
