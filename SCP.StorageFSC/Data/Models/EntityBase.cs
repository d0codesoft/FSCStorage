namespace SCP.StorageFSC.Data.Models
{
    public abstract class EntityBase
    {
        public Guid Id { get; set; } = Guid.CreateVersion7();
        public Guid PublicId { get; set; } = Guid.CreateVersion7();
        public DateTime CreatedUtc { get; set; }
        public DateTime? UpdatedUtc { get; set; }
        public Guid RowVersion { get; set; } = Guid.NewGuid();
    }
}