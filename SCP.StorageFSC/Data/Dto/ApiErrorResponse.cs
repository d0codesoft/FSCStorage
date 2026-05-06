namespace SCP.StorageFSC.Data.Dto
{
    public sealed class ApiErrorResponse
    {
        public bool Success { get; init; }
        public string ErrorCode { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
        public string? TraceId { get; init; }

        public static ApiErrorResponse Create(
            HttpContext httpContext,
            string errorCode,
            string message)
        {
            return new ApiErrorResponse
            {
                Success = false,
                ErrorCode = errorCode,
                Message = message,
                TraceId = httpContext.TraceIdentifier
            };
        }
    }
}
