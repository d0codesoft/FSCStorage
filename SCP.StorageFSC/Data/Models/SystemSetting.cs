namespace SCP.StorageFSC.Data.Models
{
    public sealed class SystemSetting : EntityBase
    {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string? Description { get; set; }
    }
}
