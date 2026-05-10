namespace fsc.mob.client.Auth;

public sealed class ConnectionProfile
{
    public string ServerUrl { get; init; } = string.Empty;
    public string? ApiToken { get; init; }
    public AuthMode AuthMode { get; init; }
    public bool HasSavedApiToken => !string.IsNullOrWhiteSpace(ApiToken);
}
