namespace SCP.StorageFSC.Data.Dto
{
    public sealed class SystemSettingDto
    {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime? UpdatedUtc { get; set; }
    }

    public sealed class UpdateSystemSettingRequest
    {
        public string Value { get; set; } = string.Empty;
    }
}
