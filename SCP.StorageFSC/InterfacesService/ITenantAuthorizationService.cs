using SCP.StorageFSC.Security;
using SCP.StorageFSC.SecurityPermission;

namespace SCP.StorageFSC.InterfacesService
{
    public interface ITenantAuthorizationService
    {
        CurrentTenantContext GetRequiredCurrentTenant();

        void DemandAuthenticated();
        void DemandAdmin();
        void DemandPermission(TenantPermission permission);

        Task DemandAdminOrSameTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
        Task DemandAdminOrSameTenantGuidAsync(Guid tenantGuid, CancellationToken cancellationToken = default);
    }
}
