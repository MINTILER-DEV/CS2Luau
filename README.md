# CS2Luau

`CS2Luau` is a Roblox-first transpiler that takes a practical subset of C# and emits readable Luau, plus an optional `.rbxmx` model and a compile-time Roblox API shim for semantic analysis and editor tooling.

## Projects

- `src/CS2Luau.Compiler`: Roslyn-backed compiler pipeline, diagnostics, IR, Luau emission, and `.rbxmx` serialization.
- `src/CS2Luau.Cli`: `build` and `compile` commands with optional runtime inclusion, XML model emission, verbose logging, and watch mode.
- `src/CS2Luau.Roblox`: compile-time-only Roblox C# API shim for globals, services, instances, datatypes, signals, and enums.
- `src/CS2Luau.Runtime`: bundled Luau runtime helpers copied alongside generated output when requested.
- `tests/CS2Luau.Tests`: intrinsics, snapshots, diagnostics, and CLI coverage.
- `examples/`: sample Roblox-flavored C# projects.

## Supported MVP surface

- namespaces and using directives
- classes, fields, auto-properties, constructors, instance methods, static methods
- locals, assignments, if/else, switch, while, do, for, foreach, return
- arithmetic and boolean operators
- enums and simple arrays
- object creation for user types and Roblox datatypes
- Roblox intrinsics such as `Globals.game`, `GetService<T>()`, `GetService(string)`, `Instance.New<T>()`, datatype constructors, enum lowering, and signal `Connect`

## Explicit diagnostics for unsupported features

The compiler currently emits structured diagnostics for unsupported async/await, LINQ query syntax, iterators, exception handling, unsafe code, partial classes, generic user-defined types/methods, extension methods, operator overloads, attributes, anonymous methods, and conditional expressions.

## Build and test

```powershell
dotnet build CS2Luau.slnx
dotnet test CS2Luau.slnx
```

## CLI usage

```powershell
dotnet run --project .\src\CS2Luau.Cli -- build --project .\examples\BasicPartExample --verbose
dotnet run --project .\src\CS2Luau.Cli -- build --project .\examples\BasicPartExample --emit-rbxmx --include-runtime
dotnet run --project .\src\CS2Luau.Cli -- compile .\examples\BasicPartExample\src\Program.cs --out .\out --verbose
```

## Project configuration

Copy `cs2luau.sample.json` and adjust it for your project:

```json
{
  "name": "MyGame",
  "sourceDir": "src",
  "outDir": "out",
  "emitRbxmx": false,
  "includeRuntime": true,
  "rootNamespace": "MyGame",
  "entrypoints": ["Program.cs"],
  "scriptType": "ModuleScript",
  "robloxApiMode": "Manual",
  "warningsAsErrors": false
}
```

## Example Roblox-flavored C#

```csharp
using static Roblox.Globals;

var players = game.GetService<Players>();
var part = Instance.New<Part>();
part.Position = new Vector3(0, 10, 0);
part.Parent = workspace;
```

## Notes

- The Roblox shim is compile-time only and intentionally throws when executed in .NET.
- The current compiler emits a single Luau file per build/compile invocation plus optional runtime modules and `.rbxmx`.
- The architecture is set up so the shim can later be generated from Roblox API metadata.
