using CommunityToolkit.Mvvm.ComponentModel;
using fsc.mob.client.Api;

namespace fsc.mob.client.ViewModels;

public abstract partial class ViewModelBase : ObservableObject
{
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial bool IsUnauthorized { get; set; }

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
