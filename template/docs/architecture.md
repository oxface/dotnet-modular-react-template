# Architecture

This template is a domain-neutral modular monolith with a .NET backend, React
frontends, and local orchestration for supporting services.

Architecture details are split by area:

- [Server](architecture/server.md)
- [Intermodule Communication](architecture/intermodule-communication.md)
- [Web](architecture/web.md)
- [Orchestration](architecture/orchestration.md)
- [Workflows](architecture/workflows.md)

The template favors explicit boundaries over framework magic. Backend modules
own domain and application behavior, infrastructure details stay behind module
interfaces, and the Host composes platform services.

Implementation progress for the generated template is indexed separately under
[current-state/](current-state/).
