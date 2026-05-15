using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using fsc.mob.client.Api;
using fsc.mob.client.Auth;
using fsc.mob.client.Services;

namespace fsc.mob.client.ViewModels;

public sealed partial class UsersViewModel : ViewModelBase
{
    private readonly FscAdminApiClient _apiClient;
    private readonly ConnectionSessionService _sessionService;
    private readonly NavigationService _navigationService;

    public UsersViewModel(FscAdminApiClient apiClient, ConnectionSessionService sessionService, NavigationService navigationService)
    {
        _apiClient = apiClient;
        _sessionService = sessionService;
        _navigationService = navigationService;
    }

    [ObservableProperty]
    public partial IReadOnlyList<UserManagementViewModel> Users { get; set; } = [];

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
            Users = await _apiClient.GetUsersAsync();
            StatusMessage = Users.Count == 0 ? "No users found." : $"Loaded {Users.Count} users.";
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
    private Task CreateUserAsync() => _navigationService.GoToUserDetailsAsync();

    [RelayCommand]
    private Task OpenUserAsync(UserManagementViewModel? user)
    {
        return user is null
            ? Task.CompletedTask
            : _navigationService.GoToUserDetailsAsync(user.UserId);
    }
}
