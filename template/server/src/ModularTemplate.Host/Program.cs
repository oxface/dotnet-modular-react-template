using System.Reflection;
using ModularTemplate.Host.Configuration;
using ModularTemplate.Host.Features.Auth;
using ModularTemplate.Persistence.Configuration;
using ModularTemplate.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
bool isOpenApiDocumentGeneration =
    Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";

if (isOpenApiDocumentGeneration
    && string.IsNullOrWhiteSpace(builder.Configuration["ConnectionStrings:modular-template-host"]))
{
    builder.Configuration["ConnectionStrings:modular-template-host"] =
        "Host=localhost;Port=5432;Database=modular_template_openapi;Username=postgres;Password=postgres";
}

builder.AddServiceDefaults();
builder.AddPersistence();
builder.AddHostAuthentication();
builder.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddModularTemplateMediator();
builder.Services.AddModularTemplateModules();

var app = builder.Build();
app.UseProblemDetails();
app.UseAuthentication();
app.UseAuthorization();
app.MapDefaultEndpoints();
app.MapAuthEndpoints();
app.MapModularTemplateModuleEndpoints();

app.Run();

public partial class Program;
