# Products Module

Products is the generated sample product module. It exists to show the module
shape a product-owned business module should follow without overlapping with
Bondstone runtime terms such as durable operation.

Products owns:

- Product aggregate state and timestamps.
- Provider-neutral product read contracts.
- Module-owned persistence for product records.
- The `GET /api/products/{productId}` endpoint.

Products does not own cross-module business workflows by default. Product
repositories should replace or extend this sample with concrete product state,
commands, events, and process managers only when accepted product behavior
defines them.

Implementation progress for the shipped template lives in
[../current-state/server.md](../current-state/server.md).
