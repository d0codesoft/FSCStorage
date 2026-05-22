namespace scp.filestorage.Data.Dto
{
    public sealed class SystemSettingDto
    {
        public string Name { get; init; } = string.Empty;
        public string? Value { get; init; }
        public string ValueType { get; init; } = "string";
        public string? Description { get; init; }
        public bool IsSecret { get; init; }
        public bool RequiresRestart { get; init; }
        public DateTime CreatedUtc { get; init; }
        public DateTime? UpdatedUtc { get; init; }
    }
}
