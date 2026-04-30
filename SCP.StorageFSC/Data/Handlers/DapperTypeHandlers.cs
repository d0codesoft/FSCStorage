using Dapper;

namespace scp.filestorage.Data.Handlers
{
    public static class DapperTypeHandlers
    {
        private static int _registered;

        public static void Register()
        {
            if (Interlocked.Exchange(ref _registered, 1) == 1)
                return;

            SqlMapper.RemoveTypeMap(typeof(Guid));
            SqlMapper.RemoveTypeMap(typeof(Guid?));

            SqlMapper.AddTypeHandler<Guid>(new GuidV7BinaryTypeHandler());
            SqlMapper.AddTypeHandler<Guid?>(new NullableGuidV7BinaryTypeHandler());
            SqlMapper.AddTypeHandler<DateTime>(new UtcDateTimeHandler());
            SqlMapper.AddTypeHandler<DateTime?>(new NullableUtcDateTimeHandler());
            SqlMapper.PurgeQueryCache();
        }
    }
}
