using SCP.StorageFSC.Data.Dto;

namespace SCP.StorageFSC.Data.Repositories
{
    public interface IStorageStatisticsRepository
    {
        Task<StorageStatisticsDto> GetAsync(
            int largestFilesLimit = 25,
            CancellationToken cancellationToken = default);
    }
}
