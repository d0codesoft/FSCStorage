using SCP.StorageFSC.Data.Models;

namespace scp.filestorage.Data.Models
{
    /// <summary>
    /// Role assigned to a user.
    /// </summary>
    public sealed class UserRole : EntityBase
    {
        /// <summary>
        /// Identifier of the user who has this role.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Identifier of the assigned role.
        /// </summary>
        public Guid RoleId { get; set; }
    }
}
