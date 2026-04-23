using System.Data;

namespace SCP.StorageFSC.Data
{
    public interface IDbConnectionFactory
    {
        IDbConnection CreateConnection();
    }
}
