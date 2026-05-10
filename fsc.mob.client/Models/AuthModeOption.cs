using fsc.mob.client.Auth;

namespace fsc.mob.client.Models;

public sealed class AuthModeOption
{
    public required string Label { get; init; }
    public required AuthMode Mode { get; init; }
}
