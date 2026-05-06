using SCP.StorageFSC.Data;
using SCP.StorageFSC.Data.Dto;
using SCP.StorageFSC.Services;

namespace SCP.StorageFSC.Security
{
    public sealed class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(
            RequestDelegate next,
            ILogger<ExceptionHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                _logger.LogInformation("Request was cancelled by the client.");
                if (!context.Response.HasStarted)
                {
                    context.Response.StatusCode = 499;
                }
            }
            catch (TenantAccessDeniedException ex)
            {
                _logger.LogWarning(ex, "Tenant access denied for request {Method} {Path}.", context.Request.Method, context.Request.Path);
                await WriteProblemDetailsAsync(
                    context,
                    StatusCodes.Status403Forbidden,
                    "AccessDenied",
                    ex.Message);
            }
            catch (RepositoryException ex)
            {
                _logger.LogError(ex, "Repository operation failed for request {Method} {Path}.", context.Request.Method, context.Request.Path);
                await WriteProblemDetailsAsync(
                    context,
                    StatusCodes.Status500InternalServerError,
                    "DatabaseFailed",
                    "An internal data access error occurred.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception for request {Method} {Path}.", context.Request.Method, context.Request.Path);
                await WriteProblemDetailsAsync(
                    context,
                    StatusCodes.Status500InternalServerError,
                    "StorageFailed",
                    "An unexpected error occurred.");
            }
        }

        private static async Task WriteProblemDetailsAsync(
            HttpContext context,
            int statusCode,
            string errorCode,
            string message)
        {
            if (context.Response.HasStarted)
                return;

            context.Response.Clear();
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";

            await context.Response.WriteAsJsonAsync(
                ApiErrorResponse.Create(context, errorCode, message),
                cancellationToken: context.RequestAborted);
        }
    }
}
