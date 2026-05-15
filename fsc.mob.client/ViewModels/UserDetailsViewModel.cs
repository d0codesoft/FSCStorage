using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using fsc.mob.client.Api;
using fsc.mob.client.Services;

namespace fsc.mob.client.ViewModels;

public sealed partial class UserDetailsViewModel : ViewModelBase
{
    private readonly FscAdminApiClient _apiClient;
    private readonly AlertService _alertService;
    private readonly NavigationService _navigationService;

    private Guid? _userId;

    public UserDetailsViewModel(FscAdminApiClient apiClient, AlertService alertService, NavigationService navigationService)
    {
        _apiClient = apiClient;
        _alertService = alertService;
        _navigationService = navigationService;
    }

    [ObservableProperty]
    public partial string PageTitle { get; set; } = "Create user";

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Email { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Password { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsActive { get; set; } = true;

    [ObservableProperty]
    public partial bool IsAdmin { get; set; }

    [ObservableProperty]
    public partial bool IsLocked { get; set; }

    [ObservableProperty]
    public partial string CreatedUtc { get; set; } = string.Empty;

    [ObservableProperty]
    public partial IReadOnlyList<TenantViewModel> Tenants { get; set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<UserTokenGroup> TokenGroups { get; set; } = [];

    public bool IsExistingUser => _userId.HasValue;
    public string BlockActionLabel => IsLocked ? "Unblock user" : "Block user";

    public async Task ApplyQueryAsync(IDictionary<string, object> query)
    {
        if (query.TryGetValue("userId", out var value) &&
            value is string rawUserId &&
            Guid.TryParse(rawUserId, out var parsedUserId))
        {
            _userId = parsedUserId;
        }
        else
        {
            _userId = null;
        }

        OnPropertyChanged(nameof(IsExistingUser));
        await LoadAsync();
    }

    public async Task LoadAsync()
    {
        ClearMessages();
        IsBusy = true;

        try
        {
            if (!_userId.HasValue)
            {
                PageTitle = "Create user";
                Name = string.Empty;
                Email = string.Empty;
                Password = string.Empty;
                IsActive = true;
                IsAdmin = false;
                IsLocked = false;
                CreatedUtc = string.Empty;
                Tenants = [];
                TokenGroups = [];
                return;
            }

            var users = await _apiClient.GetUsersAsync();
            var user = users.FirstOrDefault(item => item.UserId == _userId.Value)
                ?? throw new InvalidOperationException("User was not found.");

            PageTitle = user.UserName;
            Name = user.UserName;
            Email = user.Email ?? string.Empty;
            Password = string.Empty;
            IsActive = user.IsActive;
            IsAdmin = user.IsAdmin;
            IsLocked = user.IsLocked;
            CreatedUtc = user.CreatedUtcDisplay;
            Tenants = user.Tenants;
            TokenGroups = user.ApiTokens
                .GroupBy(token => token.TenantName)
                .Select(group => new UserTokenGroup
                {
                    TenantName = group.Key,
                    Tokens = group.OrderBy(token => token.Name).ToArray()
                })
                .ToArray();
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
    private async Task SaveAsync()
    {
        ClearMessages();
        IsBusy = true;

        try
        {
            if (string.IsNullOrWhiteSpace(Name))
                throw new InvalidOperationException("User name is required.");

            if (_userId.HasValue)
            {
                await _apiClient.UpdateUserAsync(
                    _userId.Value,
                    new UpdateUserRequestModel
                    {
                        Name = Name.Trim(),
                        Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim(),
                        Password = string.IsNullOrWhiteSpace(Password) ? null : Password,
                        IsActive = IsActive,
                        IsAdmin = IsAdmin
                    });

                StatusMessage = "User updated.";
            }
            else
            {
                if (string.IsNullOrWhiteSpace(Password))
                    throw new InvalidOperationException("Password is required for a new user.");

                var createdUser = await _apiClient.CreateUserAsync(
                    new CreateUserRequestModel
                    {
                        Name = Name.Trim(),
                        Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim(),
                        Password = Password,
                        IsActive = IsActive,
                        IsAdmin = IsAdmin
                    });

                _userId = createdUser.UserId;
                OnPropertyChanged(nameof(IsExistingUser));
                StatusMessage = "User created.";
            }

            Password = string.Empty;
            await LoadAsync();
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
    private async Task ToggleBlockAsync()
    {
        if (!_userId.HasValue)
            return;

        ClearMessages();
        IsBusy = true;

        try
        {
            if (IsLocked)
            {
                await _apiClient.UnblockUserAsync(_userId.Value);
                StatusMessage = "User unblocked.";
            }
            else
            {
                await _apiClient.BlockUserAsync(_userId.Value);
                StatusMessage = "User blocked.";
            }

            await LoadAsync();
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
    private async Task DeleteAsync()
    {
        if (!_userId.HasValue)
            return;

        var confirmed = await _alertService.ConfirmAsync(
            "Delete user",
            "Delete this user? Tenants, API keys, and files owned by the user may also be affected.");

        if (!confirmed)
            return;

        ClearMessages();
        IsBusy = true;

        try
        {
            await _apiClient.DeleteUserAsync(_userId.Value);
            await _alertService.ShowInfoAsync("User deleted", "The user was deleted.");
            await _navigationService.GoToUsersAsync();
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
    private Task AddTenantAsync()
    {
        return _navigationService.GoToTenantDetailsAsync(ownerUserId: _userId);
    }

    partial void OnIsLockedChanged(bool value)
    {
        OnPropertyChanged(nameof(BlockActionLabel));
    }
}
