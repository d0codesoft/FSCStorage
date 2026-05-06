namespace scp.filestorage.Data.Models
{
    /// <summary>
    /// Defines the status of a pending login challenge.
    /// </summary>
    public enum UserLoginChallengeStatus
    {
        /// <summary>
        /// Login challenge is waiting for two-factor verification.
        /// </summary>
        Pending = 0,

        /// <summary>
        /// Login challenge was completed successfully.
        /// </summary>
        Completed = 1,

        /// <summary>
        /// Login challenge expired before completion.
        /// </summary>
        Expired = 2,

        /// <summary>
        /// Login challenge was cancelled or invalidated.
        /// </summary>
        Cancelled = 3,

        /// <summary>
        /// Login challenge was blocked because too many failed attempts were made.
        /// </summary>
        Blocked = 4
    }
}
