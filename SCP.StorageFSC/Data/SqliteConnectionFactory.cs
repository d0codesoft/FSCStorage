using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using System.Data.Common;
using System.Data;

namespace SCP.StorageFSC.Data
{
    public sealed class SqliteConnectionFactory : IDbConnectionFactory
    {
        private readonly string _connectionString;
        private readonly ILogger<SqliteConnectionFactory> _logger;

        public SqliteConnectionFactory(
            string connectionString,
            ILogger<SqliteConnectionFactory> logger)
        {
            _connectionString = connectionString;
            _logger = logger;
        }

        public IDbConnection CreateConnection()
        {
            DbConnection connection = new SqliteConnection(_connectionString);
            return new LoggingDbConnection(connection, _logger);
        }
    }
}
