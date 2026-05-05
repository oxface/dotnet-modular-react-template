# Server Testing

The backend target uses xUnit, Shouldly, NSubstitute, Testcontainers, and
architecture tests.

Every backend test should declare a category trait so CI can choose test bands
explicitly:

```csharp
[Trait("Category", "Unit")]
```

Expected categories:

- `Unit`
- `Application`
- `Integration`
- `Eval`

Integration tests should use real IO through Testcontainers rather than an
in-memory persistence stack.
