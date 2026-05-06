# Observability

The backend uses OpenTelemetry through `ModularTemplate.ServiceDefaults`.

Current defaults:

- OpenTelemetry logging with formatted messages and scopes.
- ASP.NET Core, HTTP client, and runtime metrics.
- ASP.NET Core and HTTP client tracing.
- OTLP export when `OTEL_EXPORTER_OTLP_ENDPOINT` is configured.
- Development-only `/health` and `/alive` health endpoints for web apps that
  call `MapDefaultEndpoints`.

Future gates should document local dashboards and the boundaries between
template defaults and product-specific telemetry.
