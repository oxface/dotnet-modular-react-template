# Architecture

This template is a domain-neutral modular monolith with a .NET backend, React
frontends, and local orchestration for supporting services.

Architecture details are split by area:

- [Server](architecture/server.md)
- [Web](architecture/web.md)
- [Orchestration](architecture/orchestration.md)

The template favors explicit boundaries over framework magic. Backend modules
own domain and application behavior, infrastructure details stay behind module
interfaces, and the Host composes platform services.
