using SCP.StorageFSC.Data.Models;

namespace scp.filestorage.Data.Models
{
    /// <summary>
    /// Represents an application role.
    /// </summary>
    public sealed class Role : EntityBase
    {
        /// <summary>
        /// Role name displayed in the application.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Normalized role name used for case-insensitive search and uniqueness checks.
        /// </summary>
        public string NormalizedName { get; set; } = string.Empty;

        /// <summary>
        /// Optional role description for administrative purposes.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Indicates whether this role is a built-in system role.
        /// System roles should not be deleted by regular administrative operations.
        /// </summary>
        public bool IsSystem { get; set; }
    }
}
