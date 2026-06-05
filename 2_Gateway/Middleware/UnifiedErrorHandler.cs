using System.Text.Json;

namespace VanAn.Gateway.Middleware
{
    public class UnifiedErrorHandler(RequestDelegate next, ILogger<UnifiedErrorHandler> logger)
    {
        private readonly RequestDelegate _next = next;
        private readonly ILogger<UnifiedErrorHandler> _logger = logger;

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            ILogger<UnifiedErrorHandler> logger = context.RequestServices.GetRequiredService<ILogger<UnifiedErrorHandler>>();

            logger.LogError(exception, "An unhandled exception occurred: {Message}", exception.Message);

            context.Response.Clear();
            context.Response.ContentType = "application/json";

            ErrorResponse errorResponse = new()
            {
                ErrorId = Guid.NewGuid().ToString(),
                Message = GetErrorMessage(exception),
                Details = GetErrorDetails(exception),
                Timestamp = DateTime.UtcNow,
                Path = context.Request.Path,
                Method = context.Request.Method
            };

            context.Response.StatusCode = GetStatusCode(exception);

            JsonSerializerOptions jsonOptions = new()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            string jsonResponse = JsonSerializer.Serialize(errorResponse, jsonOptions);
            await context.Response.WriteAsync(jsonResponse);
        }

        private static string GetErrorMessage(Exception exception)
        {
            return exception switch
            {
                ArgumentNullException => "Required argument is missing",
                ArgumentException => "Invalid argument provided",
                InvalidOperationException => "Operation is not valid in the current state",
                UnauthorizedAccessException => "Access denied",
                KeyNotFoundException => "Requested resource not found",
                TimeoutException => "Operation timed out",
                _ => "An unexpected error occurred"
            };
        }

        private static string GetErrorDetails(Exception exception)
        {
            // In development, include full exception details
            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
            {
                return exception.ToString();
            }

            // In production, return generic message
            return "See application logs for more details";
        }

        private static int GetStatusCode(Exception exception)
        {
            return exception switch
            {
                ArgumentNullException => StatusCodes.Status400BadRequest,
                ArgumentException => StatusCodes.Status400BadRequest,
                InvalidOperationException => StatusCodes.Status400BadRequest,
                UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
                KeyNotFoundException => StatusCodes.Status404NotFound,
                TimeoutException => StatusCodes.Status408RequestTimeout,
                _ => StatusCodes.Status500InternalServerError
            };
        }
    }

    public class ErrorResponse
    {
        public string ErrorId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string Path { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty;
    }
}
