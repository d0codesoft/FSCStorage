namespace scp.filestorage.Data.Models
{
    public enum MultipartUploadStatus : short
    {
        Created = 0,
        Uploading = 1,
        Completing = 2,
        Completed = 3,
        Aborted = 4,
        Failed = 5,
        Expired = 6
    }

    public enum MultipartUploadPartStatus : short
    {
        Pending = 0,
        Uploaded = 1,
        Verified = 2,
        Failed = 3
    }

    public enum FileStatus : short
    {
        Active = 0,
        Deleted = 1,
        Quarantined = 2
    }

    public enum FileVisibility : short
    {
        Private = 0,
        Tenant = 1,
        Public = 2
    }
}