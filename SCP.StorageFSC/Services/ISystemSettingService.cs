using scp.filestorage.Data.Dto;

namespace scp.filestorage.Services
{
    public interface ISystemSettingService
    {
        Task<IReadOnlyList<SystemSettingDto>> GetAllAsync(CancellationToken ct = default);

        Task<SystemSettingDto?> GetByNameAsync(
            string name,
            CancellationToken ct = default);

        Task<SystemSettingDto> UpdateAsync(
            UpdateSystemSettingRequest request,
            CancellationToken ct = default);
    }
}
