using System.Collections.Concurrent;

namespace scp_fs_cli.Operations
{
    public sealed class ManagerOperation
    {
        private readonly ConcurrentDictionary<string, IUnitOperation> _operations =
            new(StringComparer.OrdinalIgnoreCase);

        public IEnumerable<IUnitOperation> Operations =>
            _operations.Values
                .OrderBy(GetDisplayOrder)
                .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase);

        public void Register(IUnitOperation operation)
        {
            ArgumentNullException.ThrowIfNull(operation);

            if (!_operations.TryAdd(operation.Key, operation))
                throw new InvalidOperationException($"Operation with key '{operation.Key}' is already registered.");
        }

        public bool TryGet(string key, out IUnitOperation? operation)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                operation = null;
                return false;
            }

            return _operations.TryGetValue(key, out operation);
        }

        private static int GetDisplayOrder(IUnitOperation operation)
        {
            return operation.Key.ToLowerInvariant() switch
            {
                "upload" => 0,
                "status" => 1,
                _ => 100
            };
        }
    }
}
