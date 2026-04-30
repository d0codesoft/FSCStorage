namespace SCP.StorageFSC.Data.Models
{
    public abstract class EntityBase
    {
        public Guid Id { get; init; } = Guid.CreateVersion7();
        public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;
        public DateTime? UpdatedUtc { get; private set; }
        public Guid RowVersion { get; private set; } = Guid.NewGuid();

        public void MarkUpdated()
        {
            UpdatedUtc = DateTime.UtcNow;
            RowVersion = Guid.NewGuid();
        }
    }
}