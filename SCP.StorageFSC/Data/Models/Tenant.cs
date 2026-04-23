namespace SCP.StorageFSC.Data.Models
{
    /// <summary>
    /// System tenant.
    /// </summary>
    public sealed class Tenant : EntityBase
    {
        /// <summary>
        /// External tenant GUID identifier passed through the API.
        /// </summary>
        public Guid TenantGuid { get; set; }

        /// <summary>
        /// Tenant code/name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Tenant active flag.
        /// </summary>
        public bool IsActive { get; set; }
    }
}
