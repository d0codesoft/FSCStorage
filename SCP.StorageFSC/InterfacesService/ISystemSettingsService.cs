using SCP.StorageFSC.Data.Dto;

namespace SCP.StorageFSC.InterfacesService
{
    public interface ISystemSettingsService
    {
        Task<IReadOnlyList<SystemSettingDto>> GetAllAsync(CancellationToken cancellationToken = default);

        Task<SystemSettingDto?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

        Task<SystemSettingDto> UpdateAsync(
            string name,
            UpdateSystemSettingRequest request,
            CancellationToken cancellationToken = default);

        Task LoadFileTransferLimiterSettingsAsync(CancellationToken cancellationToken = default);
    }
}
