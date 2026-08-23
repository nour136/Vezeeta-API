using Domain;
using Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text.Json;

namespace Vezeeta.API.Middlewares
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;
        private readonly IHostEnvironment _environment;

        public ExceptionHandlingMiddleware(
            RequestDelegate next,
            ILogger<ExceptionHandlingMiddleware> logger,
            IHostEnvironment environment)
        {
            _next = next;
            _logger = logger;
            _environment = environment;
        }

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

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var (statusCode, message) = MapException(exception);

            if (exception is AppException)
            {
                _logger.LogWarning(exception, "Handled application exception: {Message}", exception.Message);
            }
            else
            {
                _logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);
            }

            var response = new ResponseModel<object>
            {
                Success = false,
                Message = message,
                Data = _environment.IsDevelopment() && statusCode == HttpStatusCode.InternalServerError
                    ? new { exception.StackTrace }
                    : null
            };

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)statusCode;

            var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await context.Response.WriteAsync(json);
        }

        private static (HttpStatusCode StatusCode, string Message) MapException(Exception exception)
        {
            return exception switch
            {
                AppException appException => (appException.StatusCode, appException.Message),

                DbUpdateException => (HttpStatusCode.Conflict, "The operation could not be completed due to a data conflict."),

                _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred. Please try again later.")
            };
        }
    }

    public static class ExceptionHandlingMiddlewareExtensions
    {
        public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder app)
        {
            return app.UseMiddleware<ExceptionHandlingMiddleware>();
        }
    }
}
