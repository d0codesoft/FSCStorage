namespace SCP.StorageFSC.Data.Dto
{
    public enum SaveFileStatus
    {
        Success = 0,
        ValidationError = 1,
        AccessDenied = 2,
        StorageFailed = 3,
        DatabaseFailed = 4,
        DuplicateFile = 5,
        AlreadyExists = 6
    }
}
