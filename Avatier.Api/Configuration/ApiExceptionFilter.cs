using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Text.Json;

namespace Avatier.Api.Configuration
{
    public class ApiExceptionFilter : IExceptionFilter
    {
        private readonly ILogger<ApiExceptionFilter> _logger;

        public ApiExceptionFilter(ILogger<ApiExceptionFilter> logger)
        {
            _logger = logger;
        }

        public void OnException(ExceptionContext context)
        {
            var apiError = new ProblemDetails
            {
                Title = "An error occurred",
                Status = context.HttpContext.Response.StatusCode,
                Detail = context.Exception.Message
            };

           
            if (context.Exception is UnauthorizedAccessException)
            {
                apiError.Title = "Unauthorized Access";
                context.HttpContext.Response.StatusCode = 401;
            }
            else
            {
                // Unhandled errors
#if !DEBUG
            apiError.Detail = "An unhandled error occurred.";
            apiError.Extensions["stackTrace"] = null;
#else
                apiError.Extensions["stackTrace"] = context.Exception.StackTrace;
#endif

                context.HttpContext.Response.StatusCode = 500;

            }

            apiError.Status = context.HttpContext.Response.StatusCode;

            _logger.LogError(context.Exception, nameof(context.Exception.TargetSite));
            // always return a JSON result
            context.Result = new JsonResult(apiError);
        }
    }
}
