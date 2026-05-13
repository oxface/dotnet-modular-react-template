using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Diagnostics;
using ModularTemplate.SharedKernel.Validation;

namespace ModularTemplate.Host.Configuration;

public static class HostErrorHandlingConfiguration
{
    public static WebApplicationBuilder AddProblemDetails(this WebApplicationBuilder builder)
    {
        builder.Services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Instance =
                    $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}";

                context.ProblemDetails.Extensions.TryAdd(
                    "traceId",
                    context.HttpContext.TraceIdentifier);

                if (context.Exception is RequestValidationException validationException)
                {
                    context.ProblemDetails.Title = "Request validation failed.";
                    context.ProblemDetails.Status = StatusCodes.Status400BadRequest;
                    context.HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                    context.ProblemDetails.Extensions.TryAdd(
                        "errors",
                        validationException.Errors);
                }
            };
        });

        return builder;
    }

    public static WebApplication UseProblemDetails(this WebApplication app)
    {
        app.UseExceptionHandler(new ExceptionHandlerOptions
        {
            StatusCodeSelector = exception => exception is RequestValidationException
                ? StatusCodes.Status400BadRequest
                : StatusCodes.Status500InternalServerError
        });
        app.UseStatusCodePages();

        return app;
    }
}
