using SCP.StorageFSC.Data.Repositories;
using SCP.StorageFSC.InterfacesService;
using SCP.StorageFSC.Security;
using SCP.StorageFSC.SecurityPermission;

namespace SCP.StorageFSC.Services
{
    public sealed class TenantAuthorizationService : ITenantAuthorizationService
    {
        private readonly ICurrentTenantAccessor _currentTenantAccessor;
        private readonly ITenantRepository _tenantRepository;

        public TenantAuthorizationService(
            ICurrentTenantAccessor currentTenantAccessor,
            ITenantRepository tenantRepository)
        {
            _currentTenantAccessor = currentTenantAccessor;
            _tenantRepository = tenantRepository;
        }

        public CurrentTenantContext GetRequiredCurrentTenant()
        {
            return _currentTenantAccessor.GetRequired();
        }

        public void DemandAuthenticated()
        {
            _ = _currentTenantAccessor.GetRequired();
        }

        public void DemandAdmin()
        {
            var current = _currentTenantAccessor.GetRequired();

            if (!current.IsAdmin)
                throw new TenantAccessDeniedException("Administrative token is required.");
        }

        public void DemandPermission(TenantPermission permission)
        {
            var current = _currentTenantAccessor.GetRequired();

            var allowed = permission switch
            {
                TenantPermission.None => true,
                TenantPermission.Read => current.CanRead || current.IsAdmin,
                TenantPermission.Write => current.CanWrite || current.IsAdmin,
                TenantPermission.Delete => current.CanDelete || current.IsAdmin,
                TenantPermission.Admin => current.IsAdmin,
                _ => false
            };

            if (!allowed)
                throw new TenantAccessDeniedException($"Permission '{permission}' is required.");
        }

        public Task DemandAdminOrSameTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            var current = _currentTenantAccessor.GetRequired();

            if (current.IsAdmin)
                return Task.CompletedTask;

            if (current.TenantId == tenantId)
                return Task.CompletedTask;

            throw new TenantAccessDeniedException("Access denied for another tenant.");
        }

        public async Task DemandAdminOrSameTenantGuidAsync(Guid tenantGuid, CancellationToken cancellationToken = default)
        {
            var current = _currentTenantAccessor.GetRequired();

            if (current.IsAdmin)
                return;

            if (current.TenantGuid == tenantGuid)
                return;

            throw new TenantAccessDeniedException("Access denied for another tenant.");
        }
    }
}
