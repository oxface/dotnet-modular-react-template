var builder = DistributedApplication.CreateBuilder(args);

var postgresUsername = builder.AddParameter("postgres-username");
var postgresPassword = builder.AddParameter("postgres-password", secret: true);

var postgres = builder.AddPostgres("postgres", postgresUsername, postgresPassword, port: 5432)
    .WithDataVolume("postgres-data")
    .WithPgAdmin();
var database = postgres.AddDatabase("modular-template-host", "modular_template");

var sessionTickets = builder.AddRedis("session-tickets", port: 6379)
    .WithDataVolume("redis-session-data");

var serviceBus = builder.AddAzureServiceBus("service-bus")
    .RunAsEmulator();

var keycloakUsername = builder.AddParameter("keycloak-username");
var keycloakPassword = builder.AddParameter("keycloak-password", secret: true);
var keycloak = builder.AddKeycloak("keycloak", 8080, keycloakUsername, keycloakPassword)
    .WithDataVolume("keycloak-data")
    .WithRealmImport("./Realms");

var migrator = builder.AddProject<Projects.ModularTemplate_Migrator>("migrator")
    .WithReference(database)
    .WithEnvironment("Identity__InitialAdmin__Provider", "http://localhost:8080/realms/modular-template")
    .WithEnvironment("Identity__InitialAdmin__Subject", "00000000-0000-0000-0000-000000000001")
    .WaitFor(database);

var host = builder.AddProject<Projects.ModularTemplate_Host>("host")
    .WithExternalHttpEndpoints()
    .WithReference(database)
    .WithReference(sessionTickets)
    .WithReference(serviceBus)
    .WithEnvironment("Authentication__Oidc__Authority", "http://localhost:8080/realms/modular-template")
    .WithEnvironment("Authentication__Oidc__ClientId", "modular-template-host")
    .WithEnvironment("Authentication__Oidc__CallbackPath", "/auth/callback")
    .WithEnvironment("Authentication__Oidc__SignedOutCallbackPath", "/auth/signed-out")
    .WithEnvironment("Authentication__Oidc__RequireHttpsMetadata", "false")
    .WaitFor(database)
    .WaitFor(sessionTickets)
    .WaitFor(serviceBus)
    .WaitFor(keycloak)
    .WaitForCompletion(migrator);

builder.AddViteApp("admin", "../../web/apps/admin")
    .WithEndpoint("http", endpoint => endpoint.Port = 5173)
    .WithPnpm()
    .WithReference(host)
    .WithEnvironment("VITE_HOST_ORIGIN", host.GetEndpoint("http"))
    .WaitFor(host);

builder.AddViteApp("web", "../../web/apps/web")
    .WithEndpoint("http", endpoint => endpoint.Port = 5174)
    .WithPnpm()
    .WithReference(host)
    .WithEnvironment("VITE_HOST_ORIGIN", host.GetEndpoint("http"))
    .WaitFor(host);

builder.Build().Run();
