using Dapper;
using System.Data;

namespace SCP.StorageFSC.Data
{
    public abstract class DbSchemaBase : IDbSchema
    {
        public abstract int CurrentSchemaVersion { get; }

        public abstract string Name { get; }

        protected abstract string Sql { get; }

        public virtual async Task<bool> ApplyAsync(
            IDbConnection connection,
            IDbTransaction? transaction, 
            ILogger? logger,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    Sql,
                    transaction: transaction,
                    cancellationToken: cancellationToken));

                return true;
            }
            catch (Exception ex)
            {
                if (logger != null)
                {
                    logger.LogError(ex, "Failed to apply schema version {Version}: {Name}", CurrentSchemaVersion, Name);
                }
                else
                {
                    Console.Error.WriteLine($"Failed to apply schema version {CurrentSchemaVersion}: {Name}");
                    Console.Error.WriteLine(ex.ToString());
                }
            }
            return false;
        }
    }
}
