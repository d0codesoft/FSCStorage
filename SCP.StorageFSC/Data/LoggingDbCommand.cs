using Microsoft.Extensions.Logging;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace SCP.StorageFSC.Data
{
    internal sealed class LoggingDbCommand : DbCommand
    {
        private readonly DbCommand _innerCommand;
        private readonly DbConnection _connection;
        private readonly ILogger _logger;

        public LoggingDbCommand(DbCommand innerCommand, DbConnection connection, ILogger logger)
        {
            _innerCommand = innerCommand;
            _connection = connection;
            _logger = logger;
        }

        [AllowNull]
        public override string CommandText
        {
            get => _innerCommand.CommandText;
            set => _innerCommand.CommandText = value;
        }

        public override int CommandTimeout
        {
            get => _innerCommand.CommandTimeout;
            set => _innerCommand.CommandTimeout = value;
        }

        public override CommandType CommandType
        {
            get => _innerCommand.CommandType;
            set => _innerCommand.CommandType = value;
        }

        public override bool DesignTimeVisible
        {
            get => _innerCommand.DesignTimeVisible;
            set => _innerCommand.DesignTimeVisible = value;
        }

        public override UpdateRowSource UpdatedRowSource
        {
            get => _innerCommand.UpdatedRowSource;
            set => _innerCommand.UpdatedRowSource = value;
        }

        [AllowNull]
        protected override DbConnection DbConnection
        {
            get => _connection;
            set => _innerCommand.Connection = value is LoggingDbConnection loggingConnection
                ? Unwrap(loggingConnection)
                : value;
        }

        protected override DbParameterCollection DbParameterCollection => _innerCommand.Parameters;

        protected override DbTransaction? DbTransaction
        {
            get => _innerCommand.Transaction;
            set => _innerCommand.Transaction = value;
        }

        public override void Cancel()
        {
            _innerCommand.Cancel();
        }

        public override int ExecuteNonQuery()
        {
            return ExecuteWithLogging(_innerCommand.ExecuteNonQuery, nameof(ExecuteNonQuery));
        }

        public override object? ExecuteScalar()
        {
            return ExecuteWithLogging(_innerCommand.ExecuteScalar, nameof(ExecuteScalar));
        }

        public override void Prepare()
        {
            _innerCommand.Prepare();
        }

        protected override DbParameter CreateDbParameter()
        {
            return _innerCommand.CreateParameter();
        }

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            return ExecuteWithLogging(() => _innerCommand.ExecuteReader(behavior), nameof(ExecuteDbDataReader));
        }

        public override async Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken)
        {
            return await ExecuteWithLoggingAsync(() => _innerCommand.ExecuteNonQueryAsync(cancellationToken), nameof(ExecuteNonQueryAsync));
        }

        public override async Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken)
        {
            return await ExecuteWithLoggingAsync(() => _innerCommand.ExecuteScalarAsync(cancellationToken), nameof(ExecuteScalarAsync));
        }

        protected override async Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
        {
            return await ExecuteWithLoggingAsync(() => _innerCommand.ExecuteReaderAsync(behavior, cancellationToken), nameof(ExecuteDbDataReaderAsync));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _innerCommand.Dispose();
            }

            base.Dispose(disposing);
        }

        public override ValueTask DisposeAsync()
        {
            return _innerCommand.DisposeAsync();
        }

        private T ExecuteWithLogging<T>(Func<T> execute, string operationName)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var result = execute();
                stopwatch.Stop();
                LogSuccess(operationName, stopwatch.ElapsedMilliseconds);
                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                LogFailure(operationName, stopwatch.ElapsedMilliseconds, ex);
                throw;
            }
        }

        private async Task<T> ExecuteWithLoggingAsync<T>(Func<Task<T>> execute, string operationName)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var result = await execute();
                stopwatch.Stop();
                LogSuccess(operationName, stopwatch.ElapsedMilliseconds);
                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                LogFailure(operationName, stopwatch.ElapsedMilliseconds, ex);
                throw;
            }
        }

        private void LogSuccess(string operationName, long elapsedMilliseconds)
        {
            _logger.LogDebug(
                "SQL command executed successfully. Operation: {Operation}. DurationMs: {DurationMs}. CommandType: {CommandType}. Sql: {Sql}. Parameters: {Parameters}",
                operationName,
                elapsedMilliseconds,
                CommandType,
                CommandText,
                FormatParameters());
        }

        private void LogFailure(string operationName, long elapsedMilliseconds, Exception exception)
        {
            _logger.LogError(
                exception,
                "SQL command failed. Operation: {Operation}. DurationMs: {DurationMs}. CommandType: {CommandType}. Sql: {Sql}. Parameters: {Parameters}",
                operationName,
                elapsedMilliseconds,
                CommandType,
                CommandText,
                FormatParameters());
        }

        private string FormatParameters()
        {
            if (_innerCommand.Parameters.Count == 0)
                return "[]";

            var builder = new StringBuilder();
            builder.Append('[');

            for (int i = 0; i < _innerCommand.Parameters.Count; i++)
            {
                if (_innerCommand.Parameters[i] is not DbParameter parameter)
                    continue;

                if (builder.Length > 1)
                    builder.Append(", ");

                builder.Append(parameter.ParameterName);
                builder.Append('=');
                builder.Append(FormatParameterValue(parameter.Value));
            }

            builder.Append(']');
            return builder.ToString();
        }

        private static string FormatParameterValue(object? value)
        {
            return value switch
            {
                null or DBNull => "null",
                byte[] bytes => $"byte[{bytes.Length}]",
                string text when text.Length > 256 => $"\"{text[..256]}...\"",
                string text => $"\"{text}\"",
                DateTime dateTime => dateTime.ToString("O"),
                DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O"),
                _ => Convert.ToString(value) ?? string.Empty
            };
        }

        private static DbConnection Unwrap(LoggingDbConnection connection)
        {
            var field = typeof(LoggingDbConnection).GetField("_innerConnection", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return (DbConnection)field!.GetValue(connection)!;
        }
    }
}
