using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using fsc.mob.client.Api;
using fsc.mob.client.Auth;

namespace fsc.mob.client.ViewModels;

public sealed partial class DashboardViewModel : ViewModelBase
{
    private readonly FscAdminApiClient _apiClient;
    private readonly ConnectionSessionService _sessionService;

    public DashboardViewModel(FscAdminApiClient apiClient, ConnectionSessionService sessionService)
    {
        _apiClient = apiClient;
        _sessionService = sessionService;
    }

    [ObservableProperty]
    private string _serverUrl = string.Empty;

    [ObservableProperty]
    private string _usedBytes = "-";

    [ObservableProperty]
    private string _storedFileCount = "-";

    [ObservableProperty]
    private string _tenantFileCount = "-";

    [ObservableProperty]
    private string _tenantCount = "-";

    [ObservableProperty]
    private IReadOnlyList<LargestFileViewModel> _largestFiles = [];

    [ObservableProperty]
    private IReadOnlyList<TenantStorageStatisticsViewModel> _tenantStatistics = [];

    public async Task LoadAsync()
    {
        await _sessionService.EnsureInitializedAsync();
        ServerUrl = _sessionService.GetProfile().ServerUrl;
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
            var statistics = await _apiClient.GetStorageStatisticsAsync();
            UsedBytes = DisplayFormatting.FormatBytes(statistics.UsedBytes);
            StoredFileCount = statistics.StoredFileCount.ToString("N0");
            TenantFileCount = statistics.TenantFileCount.ToString("N0");
            TenantCount = statistics.Tenants.Count(tenant => tenant.IsActive).ToString("N0");
            LargestFiles = statistics.LargestFiles;
            TenantStatistics = statistics.Tenants;
            StatusMessage = $"Last refreshed at {DateTime.UtcNow:HH:mm:ss} UTC.";
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
