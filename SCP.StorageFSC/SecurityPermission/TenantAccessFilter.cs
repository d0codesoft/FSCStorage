using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using scp.filestorage.Security;
using SCP.StorageFSC.Data.Dto;
using SCP.StorageFSC.Data.Models;
using SCP.StorageFSC.InterfacesService;
using SCP.StorageFSC.Services;

namespace SCP.StorageFSC.SecurityPermission
{
    public sealed class TenantAccessFilter : IAsyncActionFilter
    {
        private readonly ITenantAuthorizationService _authorizationService;
        private readonly TenantAccessOptions _options;

        public TenantAccessFilter(
            ITenantAuthorizationService authorizationService,
            TenantAccessOptions options)
        {
            _authorizationService = authorizationService;
            _options = options;
        }

        public async Task OnActionExecutionAsync(
            ActionExecutingContext context,
            ActionExecutionDelegate next)
        {
            var user = context.HttpContext.User;
            var authType = user!=null ? user.FindFirst("auth_type")?.Value : null;

            if (authType == AuthType.WebApp)
            {
                await next();
                return;
            }

            try
            {
                _authorizationService.DemandAuthenticated();
 
                if (_options.RequiredPermission != TenantPermission.None)
                {
                    _authorizationService.DemandPermission(_options.RequiredPermission);
                }

                switch (_options.AccessMode)
                {
                    case TenantAccessMode.Authenticated:
                        break;

                    case TenantAccessMode.AdminOnly:
                        _authorizationService.DemandAdmin();
                        break;

                    case TenantAccessMode.AdminOrSameTenant:
                        if (!string.IsNullOrWhiteSpace(_options.RouteParameterName))
                        {
                            if (!TryGetGuid(context, _options.RouteParameterName!, out var tenantId))
                            {
                                context.Result = new BadRequestObjectResult(
                                    ApiErrorResponse.Create(context.HttpContext, "ValidationError", $"Route parameter '{_options.RouteParameterName}' is missing or invalid."));
                                return;
                            }

                            await _authorizationService.DemandAdminOrSameTenantAsync(
                                tenantId,
                                context.HttpContext.RequestAborted);
                        }
                        else if (!string.IsNullOrWhiteSpace(_options.RouteParameterGuidName))
                        {
                            if (!TryGetGuid(context, _options.RouteParameterGuidName!, out var tenantGuid))
                            {
                                context.Result = new BadRequestObjectResult(
                                    ApiErrorResponse.Create(context.HttpContext, "ValidationError", $"Route parameter '{_options.RouteParameterGuidName}' is missing or invalid."));
                                return;
                            }

                            await _authorizationService.DemandAdminOrSameTenantGuidAsync(
                                tenantGuid,
                                context.HttpContext.RequestAborted);
                        }
                        else
                        {
                            context.Result = new BadRequestObjectResult(
                                ApiErrorResponse.Create(context.HttpContext, "ValidationError", "TenantAccessMode.AdminOrSameTenant requires route parameter configuration."));
                            return;
                        }

                        break;

                    default:
                        context.Result = new ForbidResult();
                        return;
                }

                await next();
            }
            catch (TenantAccessDeniedException ex)
            {
                context.Result = new ObjectResult(ApiErrorResponse.Create(
                    context.HttpContext,
                    "AccessDenied",
                    ex.Message))
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
            }
        }

        private static bool TryGetGuid(ActionExecutingContext context, string name, out Guid value)
        {
            value = default;

            if (context.RouteData.Values.TryGetValue(name, out var routeValue) &&
                Guid.TryParse(routeValue?.ToString(), out value))
            {
                return true;
            }

            if (context.ActionArguments.TryGetValue(name, out var argValue))
            {
                switch (argValue)
                {
                    case Guid g:
                        value = g;
                        return true;
                    case string s when Guid.TryParse(s, out value):
                        return true;
                }
            }

            return false;
        }
    }
}
