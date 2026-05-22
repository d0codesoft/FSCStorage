namespace scp.filestorage.Data.Dto
{
    public sealed class UpdateSystemSettingRequest
    {
        public string Name { get; init; } = string.Empty;
        public string? Value { get; init; }
    }
}
