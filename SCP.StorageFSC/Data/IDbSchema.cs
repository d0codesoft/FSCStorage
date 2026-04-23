using System.Data;

namespace SCP.StorageFSC.Data
{
    /// <summary>
    /// Database schema contract.
    /// Allows replacing the schema implementation and using it in DbInitializer.
    /// </summary>
    public interface IDbSchema
    {
        /// <summary>
        /// Schema version applied by this class.
        /// </summary>
        int CurrentSchemaVersion { get; }

        /// <summary>
        /// Migration/version name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Applies schema changes.
        /// </summary>
        Task<bool> ApplyAsync(IDbConnection connection, IDbTransaction? transaction, ILogger? logger, CancellationToken cancellationToken = default);
    }
}
