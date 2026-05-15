using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using fsc.mob.client.Api;
using fsc.mob.client.Auth;
using fsc.mob.client.Models;
using fsc.mob.client.Services;

namespace fsc.mob.client.ViewModels;

public sealed partial class ConnectionViewModel : ViewModelBase
{
    private readonly FscAdminApiClient _apiClient;
    private readonly ConnectionSessionService _sessionService;
    private readonly NavigationService _navigationService;
    private readonly AlertService _alertService;
    private readonly ConnectivityService _connectivityService;

    private string? _pendingChallengeToken;

    public ConnectionViewModel(
        FscAdminApiClient apiClient,
        ConnectionSessionService sessionService,
        NavigationService navigationService,
        AlertService alertService,
        ConnectivityService connectivityService)
    {
        _apiClient = apiClient;
        _sessionService = sessionService;
        _navigationService = navigationService;
        _alertService = alertService;
        _connectivityService = connectivityService;

        AuthModes =
        [
            new AuthModeOption { Label = "API token", Mode = AuthMode.ApiToken },
            new AuthModeOption { Label = "Admin credentials", Mode = AuthMode.AdminCredentials }
        ];

        SelectedAuthMode = AuthModes[0];
        ServerUrl = ConnectionSessionService.GetDefaultServerUrl();
        RememberApiToken = true;
        RememberServerUrl = true;
    }

    public IReadOnlyList<AuthModeOption> AuthModes { get; }

    [ObservableProperty]
    public partial AuthModeOption SelectedAuthMode { get; set; } = null!;

    [ObservableProperty]
    public partial string ServerUrl { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ApiToken { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Login { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Password { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TwoFactorCode { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TwoFactorHint { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool RememberApiToken { get; set; }

    [ObservableProperty]
    public partial bool RememberServerUrl { get; set; }

    public bool IsApiTokenMode => SelectedAuthMode.Mode == AuthMode.ApiToken;
    public bool IsCredentialMode => SelectedAuthMode.Mode == AuthMode.AdminCredentials;
    public bool IsAwaitingTwoFactor => !string.IsNullOrWhiteSpace(_pendingChallengeToken);

    partial void OnSelectedAuthModeChanged(AuthModeOption value)
    {
        OnPropertyChanged(nameof(IsApiTokenMode));
        OnPropertyChanged(nameof(IsCredentialMode));
    }

    public async Task LoadAsync()
    {
        await _sessionService.EnsureInitializedAsync();
        var profile = _sessionService.GetProfile();

        ServerUrl = string.IsNullOrWhiteSpace(profile.ServerUrl)
            ? ConnectionSessionService.GetDefaultServerUrl()
            : profile.ServerUrl;

        ApiToken = profile.ApiToken ?? string.Empty;
        SelectedAuthMode = AuthModes.First(mode => mode.Mode == profile.AuthMode);
        RememberApiToken = profile.HasSavedApiToken;
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        if (!_connectivityService.HasNetworkAccess)
        {
            ErrorMessage = "The device is offline. Check connectivity and try again.";
            return;
        }

        ClearMessages();
        IsBusy = true;

        try
        {
            var normalizedUrl = ValidateServerUrl();
            if (IsApiTokenMode)
            {
                if (string.IsNullOrWhiteSpace(ApiToken))
                    throw new InvalidOperationException("Enter an API token.");

                await _apiClient.TestApiTokenAsync(normalizedUrl, ApiToken);
                StatusMessage = "Connection test succeeded with the provided API token.";
            }
            else
            {
                if (string.IsNullOrWhiteSpace(Login) || string.IsNullOrWhiteSpace(Password))
                    throw new InvalidOperationException("Enter both login and password.");

                var signInResult = await _apiClient.SignInAsync(normalizedUrl, Login, Password, null);
                if (signInResult.RequiresTwoFactor)
                {
                    StatusMessage = $"Server reached. Two-factor verification is required via {signInResult.TwoFactorMethod}.";
                    return;
                }

                if (!string.IsNullOrWhiteSpace(signInResult.SessionCookieHeader))
                    await _apiClient.SignOutAsync(normalizedUrl, signInResult.SessionCookieHeader);

                StatusMessage = "Connection test succeeded with the provided admin credentials.";
            }
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
    private async Task SignInAsync()
    {
        ClearMessages();
        IsBusy = true;

        try
        {
            var normalizedUrl = ValidateServerUrl();

            if (IsApiTokenMode)
            {
                if (string.IsNullOrWhiteSpace(ApiToken))
                    throw new InvalidOperationException("Enter an API token.");

                await _apiClient.TestApiTokenAsync(normalizedUrl, ApiToken);
                await _sessionService.SaveApiTokenSessionAsync(normalizedUrl, ApiToken, RememberApiToken);
                Password = string.Empty;
                TwoFactorCode = string.Empty;
                StatusMessage = "Connected with API token.";
                await _navigationService.GoToDashboardAsync();
                return;
            }

            if (IsAwaitingTwoFactor)
            {
                if (string.IsNullOrWhiteSpace(TwoFactorCode))
                    throw new InvalidOperationException("Enter the two-factor verification code.");

                var verifyResult = await _apiClient.VerifyTwoFactorAsync(normalizedUrl, _pendingChallengeToken!, TwoFactorCode);
                if (string.IsNullOrWhiteSpace(verifyResult.SessionCookieHeader))
                    throw new InvalidOperationException("The server did not return an authenticated session.");

                await _sessionService.SaveCredentialSessionAsync(normalizedUrl, verifyResult.SessionCookieHeader);
                if (RememberServerUrl)
                    await _sessionService.SaveServerUrlOnlyAsync(normalizedUrl);

                ResetSecretInputs();
                StatusMessage = $"Signed in as {verifyResult.User?.Name}.";
                await _navigationService.GoToDashboardAsync();
                return;
            }

            if (string.IsNullOrWhiteSpace(Login) || string.IsNullOrWhiteSpace(Password))
                throw new InvalidOperationException("Enter both login and password.");

            var signInResult = await _apiClient.SignInAsync(normalizedUrl, Login, Password, null);
            if (signInResult.RequiresTwoFactor)
            {
                _pendingChallengeToken = signInResult.ChallengeToken;
                TwoFactorHint = $"Two-factor verification required via {signInResult.TwoFactorMethod}.";
                OnPropertyChanged(nameof(IsAwaitingTwoFactor));
                StatusMessage = "Primary credentials accepted. Enter the verification code to continue.";
                Password = string.Empty;
                return;
            }

            if (string.IsNullOrWhiteSpace(signInResult.SessionCookieHeader))
                throw new InvalidOperationException("The server did not return an authenticated session.");

            await _sessionService.SaveCredentialSessionAsync(normalizedUrl, signInResult.SessionCookieHeader);
            if (RememberServerUrl)
                await _sessionService.SaveServerUrlOnlyAsync(normalizedUrl);

            ResetSecretInputs();
            StatusMessage = $"Signed in as {signInResult.User?.Name}.";
            await _navigationService.GoToDashboardAsync();
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
    private async Task DisconnectAsync()
    {
        ClearMessages();

        try
        {
            if (_sessionService.TryGetSessionCookieHeader(out var cookieHeader) &&
                !string.IsNullOrWhiteSpace(cookieHeader) &&
                !string.IsNullOrWhiteSpace(_sessionService.ServerUrl))
            {
                await _apiClient.SignOutAsync(_sessionService.ServerUrl, cookieHeader);
            }
        }
        catch
        {
            // Best-effort server logout.
        }

        await _sessionService.DisconnectAsync(clearSavedToken: IsApiTokenMode);
        _pendingChallengeToken = null;
        OnPropertyChanged(nameof(IsAwaitingTwoFactor));
        TwoFactorHint = string.Empty;
        StatusMessage = "Disconnected from the server.";
    }

    [RelayCommand]
    private async Task ClearSavedTokenAsync()
    {
        var confirmed = await _alertService.ConfirmAsync(
            "Clear saved token",
            "Remove the saved API token from secure storage on this device?");

        if (!confirmed)
            return;

        await _sessionService.DisconnectAsync(clearSavedToken: true);
        ApiToken = string.Empty;
        StatusMessage = "Saved API token removed.";
    }

    private string ValidateServerUrl()
    {
        var normalizedUrl = ConnectionSessionService.NormalizeServerUrl(ServerUrl);
        return normalizedUrl
            ?? throw new InvalidOperationException("Enter a valid absolute server URL.");
    }

    private void ResetSecretInputs()
    {
        _pendingChallengeToken = null;
        OnPropertyChanged(nameof(IsAwaitingTwoFactor));
        TwoFactorHint = string.Empty;
        Password = string.Empty;
        TwoFactorCode = string.Empty;
    }
}
