using SCP.StorageFSC.Data.Models;

namespace SCP.StorageFSC.Data.Repositories
{
    public interface ISystemSettingRepository
    {
        Task<IReadOnlyList<SystemSetting>> GetAllAsync(CancellationToken cancellationToken = default);

        Task<SystemSetting?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

        Task UpsertAsync(SystemSetting setting, CancellationToken cancellationToken = default);
    }
}
