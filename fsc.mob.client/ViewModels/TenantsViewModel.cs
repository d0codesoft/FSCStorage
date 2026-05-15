using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using fsc.mob.client.Api;
using fsc.mob.client.Auth;
using fsc.mob.client.Services;

namespace fsc.mob.client.ViewModels;

public sealed partial class TenantsViewModel : ViewModelBase
{
    private readonly FscAdminApiClient _apiClient;
    private readonly ConnectionSessionService _sessionService;
    private readonly NavigationService _navigationService;

    public TenantsViewModel(FscAdminApiClient apiClient, ConnectionSessionService sessionService, NavigationService navigationService)
    {
        _apiClient = apiClient;
        _sessionService = sessionService;
        _navigationService = navigationService;
    }

    [ObservableProperty]
    public partial IReadOnlyList<TenantViewModel> Tenants { get; set; } = [];

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
            Tenants = await _apiClient.GetTenantsAsync();
            StatusMessage = Tenants.Count == 0 ? "No tenants found." : $"Loaded {Tenants.Count} tenants.";
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
    private Task CreateTenantAsync() => _navigationService.GoToTenantDetailsAsync();

    [RelayCommand]
    private Task OpenTenantAsync(TenantViewModel? tenant)
    {
        return tenant is null
            ? Task.CompletedTask
            : _navigationService.GoToTenantDetailsAsync(tenant.Id);
    }
}
