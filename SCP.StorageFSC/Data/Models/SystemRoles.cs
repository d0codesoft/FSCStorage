namespace scp.filestorage.Data.Models
{
    /// <summary>
    /// Contains predefined system roles used by the application.
    /// </summary>
    public static class SystemRoles
    {
        /// <summary>
        /// Full system administrator role.
        /// </summary>
        public static readonly Guid AdministratorId =
            Guid.Parse("0196a5c2-1a00-7000-8000-000000000001");

        /// <summary>
        /// Administrator role limited to a specific tenant.
        /// </summary>
        public static readonly Guid TenantAdministratorId =
            Guid.Parse("0196a5c2-1a00-7000-8000-000000000002");

        /// <summary>
        /// Standard authenticated user role.
        /// </summary>
        public static readonly Guid UserId =
            Guid.Parse("0196a5c2-1a00-7000-8000-000000000003");

        /// <summary>
        /// Read-only user role.
        /// </summary>
        public static readonly Guid ReadOnlyId =
            Guid.Parse("0196a5c2-1a00-7000-8000-000000000004");

        /// <summary>
        /// Full system administrator role name.
        /// </summary>
        public const string Administrator = "Administrator";

        /// <summary>
        /// Tenant administrator role name.
        /// </summary>
        public const string TenantAdministrator = "TenantAdministrator";

        /// <summary>
        /// Standard authenticated user role name.
        /// </summary>
        public const string User = "User";

        /// <summary>
        /// Read-only user role name.
        /// </summary>
        public const string ReadOnly = "ReadOnly";

        /// <summary>
        /// Full system administrator normalized role name.
        /// </summary>
        public const string AdministratorNormalized = "ADMINISTRATOR";

        /// <summary>
        /// Tenant administrator normalized role name.
        /// </summary>
        public const string TenantAdministratorNormalized = "TENANTADMINISTRATOR";

        /// <summary>
        /// Standard authenticated user normalized role name.
        /// </summary>
        public const string UserNormalized = "USER";

        /// <summary>
        /// Read-only normalized role name.
        /// </summary>
        public const string ReadOnlyNormalized = "READONLY";
    }
}
