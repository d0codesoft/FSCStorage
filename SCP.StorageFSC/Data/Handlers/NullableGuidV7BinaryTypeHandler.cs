using Dapper;
using System.Data;

namespace scp.filestorage.Data.Handlers
{
    public sealed class NullableGuidV7BinaryTypeHandler : SqlMapper.TypeHandler<Guid?>
    {
        public override void SetValue(IDbDataParameter parameter, Guid? value)
        {
            parameter.DbType = DbType.Binary;
            parameter.Size = 16;
            parameter.Value = value.HasValue
                ? value.Value.ToByteArray(bigEndian: true)
                : DBNull.Value;
        }

        public override Guid? Parse(object value)
        {
            return value switch
            {
                null or DBNull => null,

                Guid guid => guid,

                byte[] bytes when bytes.Length == 16
                    => new Guid(bytes, bigEndian: true),

                ReadOnlyMemory<byte> memory when memory.Length == 16
                    => new Guid(memory.Span, bigEndian: true),

                string text when Guid.TryParse(text, out var guid)
                    => guid,

                _ => throw new DataException(
                    $"Cannot convert value of type '{value.GetType().FullName}' to nullable Guid.")
            };
        }
    }
}
