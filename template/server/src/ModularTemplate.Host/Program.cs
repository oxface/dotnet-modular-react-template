using ModularTemplate.Host.Configuration;
using ModularTemplate.Host.Features.Auth;
using ModularTemplate.Persistence.Configuration;
using ModularTemplate.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
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
