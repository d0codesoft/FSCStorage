using Microsoft.Extensions.Logging;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace SCP.StorageFSC.Data
{
    internal sealed class LoggingDbConnection : DbConnection
    {
        private readonly DbConnection _innerConnection;
        private readonly ILogger _logger;

        public LoggingDbConnection(DbConnection innerConnection, ILogger logger)
        {
            _innerConnection = innerConnection;
            _logger = logger;
        }

        [AllowNull]
        public override string ConnectionString
        {
            get => _innerConnection.ConnectionString;
            set => _innerConnection.ConnectionString = value;
        }

        public override string Database => _innerConnection.Database;

        public override string DataSource => _innerConnection.DataSource;

        public override string ServerVersion => _innerConnection.ServerVersion;

        public override ConnectionState State => _innerConnection.State;

        public override int ConnectionTimeout => _innerConnection.ConnectionTimeout;

        public override void ChangeDatabase(string databaseName)
        {
            _innerConnection.ChangeDatabase(databaseName);
        }

        public override void Close()
        {
            _innerConnection.Close();
            _logger.LogInformation("Database connection closed.");
        }

        public override void Open()
        {
            _innerConnection.Open();
            _logger.LogInformation("Database connection opened.");
        }

        public override Task OpenAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Opening database connection asynchronously.");
            return _innerConnection.OpenAsync(cancellationToken);
        }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        {
            _logger.LogInformation("Beginning database transaction with isolation level: {IsolationLevel}.", isolationLevel);
            return _innerConnection.BeginTransaction(isolationLevel);
        }

        protected override DbCommand CreateDbCommand()
        {
            return new LoggingDbCommand(_innerConnection.CreateCommand(), this, _logger);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _innerConnection.Dispose();
            }

            base.Dispose(disposing);
        }

        public override ValueTask DisposeAsync()
        {
            return _innerConnection.DisposeAsync();
        }
    }
}
