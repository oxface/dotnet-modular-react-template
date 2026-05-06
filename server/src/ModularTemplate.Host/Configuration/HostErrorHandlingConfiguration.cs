using Microsoft.AspNetCore.Http;

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
            };
        });

        return builder;
    }

    public static WebApplication UseProblemDetails(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages();

        return app;
    }
}
