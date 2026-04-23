using Microsoft.AspNetCore.Mvc;

namespace SCP.StorageFSC.SecurityPermission
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
    public sealed class TenantAccessAttribute : TypeFilterAttribute
    {
        public TenantAccessAttribute(
            TenantAccessMode accessMode,
            TenantPermission requiredPermission = TenantPermission.None,
            string? routeParameterName = null,
            string? routeParameterGuidName = null)
            : base(typeof(TenantAccessFilter))
        {
            Arguments = new object[]
            {
                new TenantAccessOptions
                {
                    AccessMode = accessMode,
                    RequiredPermission = requiredPermission,
                    RouteParameterName = routeParameterName,
                    RouteParameterGuidName = routeParameterGuidName
                }
            };
        }
    }
}
