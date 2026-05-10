using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using fsc.mob.client.Api;
using fsc.mob.client.Services;

namespace fsc.mob.client.ViewModels;

public sealed partial class TenantDetailsViewModel : ViewModelBase
{
    private readonly FscAdminApiClient _apiClient;
    private readonly AlertService _alertService;
    private readonly NavigationService _navigationService;

    private Guid? _tenantId;
    private Guid? _editingTokenId;
    private Guid? _ownerUserIdFromQuery;

    public TenantDetailsViewModel(FscAdminApiClient apiClient, AlertService alertService, NavigationService navigationService)
    {
        _apiClient = apiClient;
        _alertService = alertService;
        _navigationService = navigationService;
    }

    [ObservableProperty]
    private string _pageTitle = "Create tenant";

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private bool _isActive = true;

    [ObservableProperty]
    private string _createdUtc = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<UserOption> _availableUsers = [];

    [ObservableProperty]
    private UserOption? _selectedOwner;

    [ObservableProperty]
    private IReadOnlyList<ApiTokenViewModel> _apiTokens = [];

    [ObservableProperty]
    private string _tokenName = string.Empty;

    [ObservableProperty]
    private bool _tokenCanRead = true;

    [ObservableProperty]
    private bool _tokenCanWrite;

    [ObservableProperty]
    private bool _tokenCanDelete;

    [ObservableProperty]
    private bool _tokenIsAdmin;

    [ObservableProperty]
    private bool _tokenIsActive = true;

    [ObservableProperty]
    private string _tokenExpiresUtcText = string.Empty;

    [ObservableProperty]
    private UserOption? _selectedTokenUser;

    [ObservableProperty]
    private CreatedApiTokenViewModel? _generatedToken;

    public bool IsExistingTenant => _tenantId.HasValue;
    public bool IsEditingToken => _editingTokenId.HasValue;
    public bool HasGeneratedToken => GeneratedToken is not null;

    public async Task ApplyQueryAsync(IDictionary<string, object> query)
    {
        if (query.TryGetValue("tenantId", out var tenantValue) &&
            tenantValue is string rawTenantId &&
            Guid.TryParse(rawTenantId, out var parsedTenantId))
        {
            _tenantId = parsedTenantId;
        }
        else
        {
            _tenantId = null;
        }

        if (query.TryGetValue("ownerUserId", out var ownerValue) &&
            ownerValue is string rawOwnerId &&
            Guid.TryParse(rawOwnerId, out var parsedOwnerId))
        {
            _ownerUserIdFromQuery = parsedOwnerId;
        }
        else
        {
            _ownerUserIdFromQuery = null;
        }

        OnPropertyChanged(nameof(IsExistingTenant));
        await LoadAsync();
    }

    public async Task LoadAsync()
    {
        ClearMessages();
        IsBusy = true;

        try
        {
            var users = await _apiClient.GetUsersAsync();
            AvailableUsers = users
                .OrderBy(user => user.UserName, StringComparer.OrdinalIgnoreCase)
                .Select(user => new UserOption
                {
                    UserId = user.UserId,
                    Label = $"{user.UserName} ({user.RoleLabel})"
                })
                .ToArray();

            if (!_tenantId.HasValue)
            {
                PageTitle = "Create tenant";
                Name = string.Empty;
                IsActive = true;
                CreatedUtc = string.Empty;
                SelectedOwner = AvailableUsers.FirstOrDefault(option => option.UserId == _ownerUserIdFromQuery)
                    ?? AvailableUsers.FirstOrDefault();
                ApiTokens = [];
                ResetTokenEditor();
                return;
            }

            var tenant = await _apiClient.GetTenantAsync(_tenantId.Value)
                ?? throw new InvalidOperationException("Tenant was not found.");

            PageTitle = tenant.Name;
            Name = tenant.Name;
            IsActive = tenant.IsActive;
            CreatedUtc = tenant.CreatedUtcDisplay;
            SelectedOwner = AvailableUsers.FirstOrDefault(option => option.UserId == tenant.UserId);
            ApiTokens = await _apiClient.GetTenantTokensAsync(_tenantId.Value);
            ResetTokenEditor();
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
    private async Task SaveTenantAsync()
    {
        ClearMessages();
        IsBusy = true;

        try
        {
            if (string.IsNullOrWhiteSpace(Name))
                throw new InvalidOperationException("Tenant name is required.");

            if (SelectedOwner is null)
                throw new InvalidOperationException("Select the tenant owner.");

            if (_tenantId.HasValue)
            {
                await _apiClient.UpdateTenantAsync(
                    _tenantId.Value,
                    new TenantUpsertRequest
                    {
                        UserId = SelectedOwner.UserId,
                        Name = Name.Trim(),
                        IsActive = IsActive
                    });

                StatusMessage = "Tenant updated.";
            }
            else
            {
                var createdTenant = await _apiClient.CreateTenantAsync(
                    new TenantUpsertRequest
                    {
                        UserId = SelectedOwner.UserId,
                        Name = Name.Trim(),
                        IsActive = IsActive
                    });

                _tenantId = createdTenant.Id;
                OnPropertyChanged(nameof(IsExistingTenant));
                StatusMessage = "Tenant created.";
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
    private async Task DeleteTenantAsync()
    {
        if (!_tenantId.HasValue)
            return;

        var confirmed = await _alertService.ConfirmAsync(
            "Delete tenant",
            "Delete this tenant and its related API keys?");

        if (!confirmed)
            return;

        ClearMessages();
        IsBusy = true;

        try
        {
            await _apiClient.DeleteTenantAsync(_tenantId.Value);
            await _alertService.ShowInfoAsync("Tenant deleted", "The tenant was deleted.");
            await _navigationService.GoToTenantsAsync();
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
    private async Task SaveTokenAsync()
    {
        if (!_tenantId.HasValue)
            throw new InvalidOperationException("Save the tenant before creating API keys.");

        ClearMessages();
        IsBusy = true;

        try
        {
            if (string.IsNullOrWhiteSpace(TokenName))
                throw new InvalidOperationException("Token name is required.");

            if (SelectedTokenUser is null)
                throw new InvalidOperationException("Select the user for this API key.");

            var expiresUtc = ParseUtcDate(TokenExpiresUtcText);

            if (_editingTokenId.HasValue)
            {
                await _apiClient.UpdateApiTokenAsync(
                    _editingTokenId.Value,
                    new UpdateApiTokenRequestModel
                    {
                        Name = TokenName.Trim(),
                        CanRead = TokenCanRead,
                        CanWrite = TokenCanWrite,
                        CanDelete = TokenCanDelete,
                        IsAdmin = TokenIsAdmin,
                        IsActive = TokenIsActive,
                        ExpiresUtc = expiresUtc
                    });

                StatusMessage = "API key updated.";
            }
            else
            {
                GeneratedToken = await _apiClient.CreateApiTokenAsync(
                    new CreateApiTokenRequestModel
                    {
                        UserId = SelectedTokenUser.UserId,
                        TenantId = _tenantId.Value,
                        Name = TokenName.Trim(),
                        CanRead = TokenCanRead,
                        CanWrite = TokenCanWrite,
                        CanDelete = TokenCanDelete,
                        IsAdmin = TokenIsAdmin,
                        ExpiresUtc = expiresUtc
                    });

                OnPropertyChanged(nameof(HasGeneratedToken));
                StatusMessage = "API key created. Copy the token now; it will not be shown again.";
            }

            await RefreshTokensAsync();
            ResetTokenEditor();
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
    private void StartCreateToken()
    {
        ResetTokenEditor();
    }

    [RelayCommand]
    private void EditToken(ApiTokenViewModel? token)
    {
        if (token is null)
            return;

        _editingTokenId = token.Id;
        TokenName = token.Name;
        TokenCanRead = token.CanRead;
        TokenCanWrite = token.CanWrite;
        TokenCanDelete = token.CanDelete;
        TokenIsAdmin = token.IsAdmin;
        TokenIsActive = token.IsActive;
        TokenExpiresUtcText = token.ExpiresUtc?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty;
        SelectedTokenUser = AvailableUsers.FirstOrDefault(option => option.UserId == token.UserId)
            ?? SelectedOwner
            ?? AvailableUsers.FirstOrDefault();
        GeneratedToken = null;
        OnPropertyChanged(nameof(IsEditingToken));
        OnPropertyChanged(nameof(HasGeneratedToken));
    }

    [RelayCommand]
    private async Task DeleteTokenAsync(ApiTokenViewModel? token)
    {
        if (token is null)
            return;

        var confirmed = await _alertService.ConfirmAsync("Delete API key", $"Delete API key '{token.Name}'?");
        if (!confirmed)
            return;

        ClearMessages();
        IsBusy = true;

        try
        {
            await _apiClient.DeleteApiTokenAsync(token.Id);
            await RefreshTokensAsync();
            StatusMessage = "API key deleted.";
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
    private void ClearGeneratedToken()
    {
        GeneratedToken = null;
        OnPropertyChanged(nameof(HasGeneratedToken));
    }

    public void ClearSensitiveTokenDisplay()
    {
        GeneratedToken = null;
        OnPropertyChanged(nameof(HasGeneratedToken));
    }

    private async Task RefreshTokensAsync()
    {
        if (_tenantId.HasValue)
            ApiTokens = await _apiClient.GetTenantTokensAsync(_tenantId.Value);
    }

    private void ResetTokenEditor()
    {
        _editingTokenId = null;
        TokenName = string.Empty;
        TokenCanRead = true;
        TokenCanWrite = false;
        TokenCanDelete = false;
        TokenIsAdmin = false;
        TokenIsActive = true;
        TokenExpiresUtcText = string.Empty;
        SelectedTokenUser = SelectedOwner ?? AvailableUsers.FirstOrDefault();
        OnPropertyChanged(nameof(IsEditingToken));
    }

    private static DateTime? ParseUtcDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return DateTime.TryParse(
            value.Trim(),
            null,
            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
            out var parsedUtc)
            ? parsedUtc
            : throw new InvalidOperationException("Expiration date must be a valid UTC value.");
    }
}
