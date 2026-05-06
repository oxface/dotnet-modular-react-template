using ModularTemplate.Host.Configuration;
using ModularTemplate.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddProblemDetails();

var app = builder.Build();
app.UseProblemDetails();
app.MapDefaultEndpoints();

app.Run();
