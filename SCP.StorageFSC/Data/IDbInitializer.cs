namespace SCP.StorageFSC.Data
{
    public interface IDbInitializer
    {
        Task InitializeAsync(CancellationToken cancellationToken = default);
    }
}
