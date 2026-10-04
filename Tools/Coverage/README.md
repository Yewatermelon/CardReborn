# Off-Unity coverage harness (M1-R2)

Runs the `Card.Core` EditMode tests **without Unity**, then reports line coverage for
`Assets/_Project/0_Core`.

```powershell
powershell -ExecutionPolicy Bypass -File Tools/coverage.ps1
```

Why this exists:

1. The M1 gate requires `Card.Core` line coverage >= 90%, and Unity's own coverage package
   needs a registry download while this harness works with the local NuGet cache;
2. Compiling the kernel here proves FR-13.2 / FR-13.3 (the rule kernel builds and runs in a
   plain .NET process), which is the foundation of `Card.Server` in M11;
3. It is dev-only tooling: nothing under `Assets/` or `Packages/` changes.

Notes:

- Two projects: `CardReborn.Kernel` (links `Assets/_Project/0_Core`) and
  `CardReborn.Kernel.Tests` (links `Assets/_Project/7_Tests/EditMode`). The split matters:
  coverlet does not instrument the test assembly itself, so the kernel must be its own library
  for the report to contain anything.
- `7_Tests/EditMode/Infrastructure/**` is excluded: this harness only proves the **Unity-free
  kernel** (Core / Domain / Application) builds and passes, and `Card.Infrastructure` is allowed
  to use Unity. Those tests run in the Unity Test Runner instead.
- `LangVersion` is pinned to 9.0 to match Unity 2022.3, so a green run here also means the
  kernel compiles under the editor's language version.
