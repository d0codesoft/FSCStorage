namespace SCP.StorageFSC.InterfacesService
{
    public sealed class DeletedTenantCleanupResult
    {
        public int PendingTenantCount { get; set; }
        public int CleanedTenantCount { get; set; }
        public int DeletedFileCount { get; set; }
    }

    public interface IDeletedTenantCleanupService
    {
        Task<DeletedTenantCleanupResult> CleanupAsync(CancellationToken cancellationToken = default);
    }
}
