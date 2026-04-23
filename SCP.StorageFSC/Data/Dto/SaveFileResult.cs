namespace SCP.StorageFSC.Data.Dto
{
    public sealed class SaveFileResult
    {
        public bool Success { get; init; }
        public SaveFileStatus Status { get; init; }
        public string? ErrorCode { get; init; }
        public string? ErrorMessage { get; init; }
        public StoredTenantFileDto? File { get; init; }
        public bool IsDeduplicated { get; init; }
        public bool AlreadyExistsForTenant { get; init; }

        public static SaveFileResult Ok(
            StoredTenantFileDto file,
            bool isDeduplicated = false,
            bool alreadyExistsForTenant = false,
            SaveFileStatus status = SaveFileStatus.Success)
        {
            return new SaveFileResult
            {
                Success = true,
                Status = status,
                File = file,
                IsDeduplicated = isDeduplicated,
                AlreadyExistsForTenant = alreadyExistsForTenant
            };
        }

        public static SaveFileResult Fail(
            SaveFileStatus status,
            string errorCode,
            string errorMessage)
        {
            return new SaveFileResult
            {
                Success = false,
                Status = status,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage
            };
        }
    }
}
