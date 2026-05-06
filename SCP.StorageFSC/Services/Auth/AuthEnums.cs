namespace scp.filestorage.Services.Auth
{
    /// <summary>
    /// Defines the result status of a password login operation.
    /// </summary>
    public enum AuthLoginStatus
    {
        Success = 0,
        InvalidCredentials = 1,
        UserNotFound = 2,
        UserInactive = 3,
        UserLocked = 4,
        PasswordExpired = 5,
        PasswordChangeRequired = 6,
        TwoFactorRequired = 7
    }

    /// <summary>
    /// Defines the result status of a two-factor verification operation.
    /// </summary>
    public enum TwoFactorVerifyStatus
    {
        Success = 0,
        InvalidChallenge = 1,
        ChallengeExpired = 2,
        ChallengeBlocked = 3,
        InvalidCode = 4,
        MethodUnavailable = 5,
        UserInactive = 6,
        UserLocked = 7
    }

    /// <summary>
    /// Defines the result status of a two-factor setup operation.
    /// </summary>
    public enum TwoFactorSetupStatus
    {
        Success = 0,
        UserNotFound = 1,
        UserInactive = 2,
        MethodAlreadyExists = 3,
        InvalidCode = 4
    }
}
