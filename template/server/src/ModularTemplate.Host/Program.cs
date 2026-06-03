using ModularTemplate.Host.Configuration;

var builder = WebApplication.CreateBuilder(args);
HostApplicationMode mode = HostApplicationConfiguration.DetectHostApplicationMode();

builder.ConfigureHostApplicationMode(mode);
builder.AddModularTemplateHost(mode);
var app = builder.Build();
app.UseModularTemplateHost();

app.Run();

public partial class Program;
