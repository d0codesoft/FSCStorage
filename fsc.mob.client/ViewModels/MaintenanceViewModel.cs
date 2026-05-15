using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using fsc.mob.client.Api;
using fsc.mob.client.Auth;

namespace fsc.mob.client.ViewModels;

public sealed partial class MaintenanceViewModel : ViewModelBase
{
    private readonly FscAdminApiClient _apiClient;
    private readonly ConnectionSessionService _sessionService;

    public MaintenanceViewModel(FscAdminApiClient apiClient, ConnectionSessionService sessionService)
    {
        _apiClient = apiClient;
        _sessionService = sessionService;
    }

    [ObservableProperty]
    public partial IReadOnlyList<BackgroundTaskViewModel> ActiveTasks { get; set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<BackgroundTaskViewModel> CompletedTasks { get; set; } = [];

    public async Task LoadAsync()
    {
        await _sessionService.EnsureInitializedAsync();
        if (_sessionService.IsAuthenticated)
            await RefreshAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        ClearMessages();
        IsBusy = true;

        try
        {
            ActiveTasks = await _apiClient.GetActiveBackgroundTasksAsync();
            CompletedTasks = await _apiClient.GetCompletedBackgroundTasksAsync();
            StatusMessage = "Background tasks refreshed.";
        }
        catch (Exception exception)
        {
            SetError(exception);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task TriggerConsistencyCheckAsync()
    {
        await RunTaskActionAsync(_apiClient.QueueConsistencyCheckAsync, "Consistency check queued.");
    }

    [RelayCommand]
    private async Task TriggerDeletedTenantCleanupAsync()
    {
        await RunTaskActionAsync(_apiClient.QueueDeletedTenantCleanupAsync, "Deleted-tenant cleanup queued.");
    }

    private async Task RunTaskActionAsync(Func<CancellationToken, Task> action, string successMessage)
    {
        ClearMessages();
        IsBusy = true;

        try
        {
            await action(CancellationToken.None);
            StatusMessage = successMessage;
            await RefreshAsync();
        }
        catch (Exception exception)
        {
            SetError(exception);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
