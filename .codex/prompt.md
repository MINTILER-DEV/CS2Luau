You are building a production-quality transpiler/compiler named **CS2Luau**.

Goal:
Create a full Roblox-focused compiler that transpiles a practical subset of **C#** into **Luau**, with a bundled runtime/API package so users can write Roblox code in C# syntax such as:

    using static Roblox.Globals;

    var ReplicatedStorage = game.GetService<ReplicatedStorage>();
    var Players = game.GetService("Players");
    var part = Instance.New<Part>();
    part.Position = new Vector3(0, 10, 0);
    part.Parent = workspace;

The compiler must NOT attempt to run Roblox from .NET. The Roblox C# package is a compile-time API shim used for typechecking, intellisense, semantic analysis, and compiler rewrites. The output is Luau source code and optionally an .rbxmx model containing the generated scripts and runtime.

Build the full solution with clean architecture, comments, tests, diagnostics, and example projects.

==================================================
HIGH-LEVEL PRODUCT REQUIREMENTS
==================================================

Create a solution with these major components:

1. **CS2Luau.Compiler**
   - Main compiler/transpiler library.
   - Uses Roslyn to parse and analyze C#.
   - Builds a semantic model.
   - Lowers C# into an internal IR.
   - Generates Luau code from that IR.
   - Produces diagnostics for unsupported features.

2. **CS2Luau.Cli**
   - Command-line tool.
   - Can compile a project or a single file.
   - Can emit Luau files to disk.
   - Can optionally emit an .rbxmx model with generated scripts plus runtime attached.
   - Has flags like:
     - `build`
     - `compile`
     - `--project`
     - `--out`
     - `--emit-rbxmx`
     - `--include-runtime`
     - `--watch`
     - `--verbose`

3. **CS2Luau.Roblox**
   - C# API shim package for Roblox.
   - Contains typed definitions for common Roblox globals, services, instances, datatypes, signals, and enums.
   - These methods/properties may throw NotSupportedException because they are compile-time only.
   - Must support both generic and string forms of GetService:
       - `game.GetService<ReplicatedStorage>()`
       - `game.GetService("ReplicatedStorage")`
   - The compiler must understand both.

4. **CS2Luau.Runtime**
   - Luau runtime helper modules for features that need runtime support.
   - Includes class support, inheritance helpers, maybe List helpers, Dictionary helpers, signal helpers if needed.
   - Keep runtime small and readable.
   - Only include helpers actually needed by emitted code.

5. **CS2Luau.Tests**
   - Unit tests for parser/semantic lowering/codegen.
   - Golden snapshot tests for C# input -> Luau output.
   - Diagnostics tests.
   - API shim recognition tests.

6. **examples/**
   - Example Roblox-style C# projects demonstrating usage.

==================================================
CORE DESIGN PRINCIPLES
==================================================

- Roblox-first, not general .NET-first.
- Subset-first: implement a realistic, useful subset of C# instead of pretending to support everything.
- Use **Roslyn** for parsing and semantic analysis.
- Use **symbol-based rewrites**, never fragile text replacement.
- Clean separation between:
  - syntax/semantic analysis
  - IR lowering
  - Luau code generation
  - diagnostics
  - project/output packaging
- Generated Luau should be readable and reasonably idiomatic.
- Unsupported features must fail with helpful diagnostics, not cryptic crashes.

==================================================
SUPPORTED C# SUBSET FOR MVP / V1
==================================================

Support these features: (Yes we can make a runtime Luau module for this)
Add runtime support for things that arent in Luau

- namespaces
- using directives
- classes
- fields
- properties (auto-properties may lower to fields or getter/setter backing fields)
- constructors
- instance methods
- static methods
- local variables
- assignments
- if / else
- switch (at least basic lowering)
- while / do / for / foreach where practical
- return
- arithmetic and boolean operators
- enums
- simple arrays
- object creation
- member access
- invocation
- lambdas for simple callback lowering
- simple inheritance
- access modifiers for compile-time semantics
- comments preservation where practical is nice but optional
- async/await
- partial classes
- full exception model
- advanced generics
- LINQ query syntax
- iterators/yield
- expression trees

Defer or explicitly reject with diagnostics:
- reflection
- dynamic
- unsafe
- operator overloading
- extension methods unless very intentionally supported
- full overload resolution beyond what Roslyn already provides for selected cases
- attributes except if adding a few compiler-specific ones later

==================================================
ROBLOX API SHIM REQUIREMENTS
==================================================

Create a compile-time C# API package that allows code like this:

    using static Roblox.Globals;

    var rs1 = game.ReplicatedStorage;
    var rs2 = game.GetService<ReplicatedStorage>();
    var rs3 = game.GetService("ReplicatedStorage");

    var players = game.Players;
    var workspaceRef = workspace;

    var p = Instance.New<Part>();
    var p2 = (Part)Instance.New("Part");

    p.Anchored = true;
    p.Position = new Vector3(0, 5, 0);
    p.Parent = workspace;

    p.Touched.Connect(hit => {
        print(hit.Name);
    });

Define these namespaces/types at minimum:

- `Roblox.Globals`
- `Roblox.Instance`
- `Roblox.DataModel`
- `Roblox.Workspace`
- `Roblox.Services.Players`
- `Roblox.Services.ReplicatedStorage`
- `Roblox.Services.RunService`
- `Roblox.Instances.Part`
- `Roblox.Instances.Folder`
- `Roblox.Instances.Model`
- `Roblox.Instances.RemoteEvent`
- `Roblox.Instances.RemoteFunction`
- `Roblox.Instances.BindableEvent`
- `Roblox.Instances.BindableFunction`
- `Roblox.Datatypes.Vector2`
- `Roblox.Datatypes.Vector3`
- `Roblox.Datatypes.CFrame`
- `Roblox.Datatypes.Color3`
- `Roblox.Datatypes.UDim`
- `Roblox.Datatypes.UDim2`
- `Roblox.Signals.RBXScriptSignal<T...>`
- `Roblox.Signals.RBXScriptConnection`
- useful enums such as Material, PartType, KeyCode, UserInputType

Use `NotSupportedException` bodies or equivalent stub bodies in compile-time API methods. They are not for runtime execution.

==================================================
REWRITE / INTRINSIC RULES
==================================================

Implement compiler-known intrinsics for the Roblox shim.

1. Global access:
   - `Globals.game` -> `game`
   - `Globals.workspace` -> `workspace`
   - `Globals.script` -> `script`

2. Direct property access:
   - `game.ReplicatedStorage` -> `game.ReplicatedStorage`
   - `game.Players` -> `game.Players`

3. GetService generic:
   - `game.GetService<ReplicatedStorage>()` -> `game:GetService("ReplicatedStorage")`
   - `game.GetService<Players>()` -> `game:GetService("Players")`

4. GetService string:
   - `game.GetService("ReplicatedStorage")` -> `game:GetService("ReplicatedStorage")`
   - preserve the string literal directly
   - if a non-literal string is passed, still emit it safely if desired, but also consider warning if it loses static typing

5. Instance creation:
   - `Instance.New<Part>()` -> `Instance.new("Part")`
   - `Instance.New("Part")` -> `Instance.new("Part")`
   - `new Vector3(x, y, z)` -> `Vector3.new(x, y, z)`
   - same for CFrame, Color3, UDim, UDim2, Vector2

6. Method calls on Roblox instances:
   - C# instance call syntax should map to Luau method-call syntax where appropriate:
       `part.Destroy()` -> `part:Destroy()`
       `remote.FireServer(x)` -> `remote:FireServer(x)`
   - Property access remains dot syntax.
   - Distinguish methods from properties based on semantic symbols and known Roblox API metadata.

7. Signal connections:
   - `part.Touched.Connect(hit => { ... })` -> `part.Touched:Connect(function(hit) ... end)`
   - support simple lambda lowering

8. Enum lowering:
   - `Material.Neon` -> `Enum.Material.Neon`
   - `PartType.Ball` -> `Enum.PartType.Ball`

9. print/task helpers:
   - if exposing helpers in shim, map cleanly:
       `print(x)` -> `print(x)`
       optional `task.Wait()` -> `task.wait()`

==================================================
SEMANTIC ANALYSIS REQUIREMENTS
==================================================

Use Roslyn SemanticModel heavily.

Do not detect special cases by raw text. Detect by resolved symbols:
- exact type symbols
- exact method symbols
- exact property symbols
- exact namespaces when needed

Create a robust intrinsic matcher system, for example:
- `IRobloxIntrinsic`
- `IntrinsicMatchContext`
- `IntrinsicEmitter`
or a similarly clean design.

The compiler must be able to answer:
- Is this invocation Roblox.DataModel.GetService<T>()?
- Is this invocation Roblox.DataModel.GetService(string)?
- Is this object creation a Roblox datatype constructor that should become `.new(...)`?
- Is this enum a Roblox enum wrapper?
- Is this property one of the Globals shim properties?

==================================================
INTERMEDIATE REPRESENTATION (IR)
==================================================

Create a compiler IR that is simpler than Roslyn syntax and easier to emit as Luau.

Example IR concepts:
- IrProgram
- IrModule
- IrClass
- IrField
- IrProperty
- IrMethod
- IrStatement
- IrExpression
- IrInvocation
- IrMemberAccess
- IrAssignment
- IrIf
- IrLoop
- IrReturn
- IrLiteral
- IrObjectCreation
- IrLambda

Include source locations / syntax references for diagnostics where helpful.

The lowering pipeline should roughly be:

Roslyn Syntax + SemanticModel
-> semantic lowering
-> custom IR
-> Luau codegen

==================================================
LUAU CODE GENERATION REQUIREMENTS
==================================================

Generate clean Luau, not messy machine sludge.

Examples of desired output style:

    local part = Instance.new("Part")
    part.Name = "Hello"
    part.Anchored = true
    part.Position = Vector3.new(0, 10, 0)
    part.Parent = workspace

For classes, generate metatable-based Luau class structures, e.g.:

    local MyClass = {}
    MyClass.__index = MyClass

    function MyClass.new(...)
        local self = setmetatable({}, MyClass)
        ...
        return self
    end

    function MyClass:MethodName(...)
        ...
    end

Support static methods and fields in a reasonable way.

For inheritance, emit Luau helpers or runtime-based inheritance.

Keep generated output deterministic for snapshot tests.

==================================================
RUNTIME REQUIREMENTS
==================================================

Create a small Luau runtime package for features the emitted code needs, such as:
- class helpers
- inheritance helpers
- optional list helpers
- optional dictionary helpers
- optional signal helpers if not directly using Roblox signals only

The runtime should be:
- modular
- readable
- minimal
- only included when needed

==================================================
RBXMX OUTPUT REQUIREMENTS
==================================================

Support optional emission of an `.rbxmx` model that includes:
- generated ModuleScripts / Scripts
- runtime modules
- correct naming/layout

Add a project packager/output module that can:
- emit flat `.luau` files
- emit a tree of files/modules
- optionally emit `.rbxmx`

If implementing `.rbxmx`, create a clean serializer for Roblox XML model format and include tests.

==================================================
PROJECT / CLI REQUIREMENTS
==================================================

Create a project format such as `cs2luau.json` with fields like:
- name
- sourceDir
- outDir
- emitRbxmx
- includeRuntime
- rootNamespace
- entrypoints
- scriptType (ModuleScript / Script / LocalScript)
- robloxApiMode
- warningsAsErrors

CLI examples:
- `cs2luau build --project ./MyGame`
- `cs2luau compile Program.cs --out out`
- `cs2luau build --emit-rbxmx`
- `cs2luau build --watch`

Provide useful terminal diagnostics and non-zero exit codes on failure.

==================================================
DIAGNOSTICS REQUIREMENTS
==================================================

Implement structured diagnostics:
- code
- message
- severity
- file
- line/column
- optional related info

Examples:
- unsupported feature diagnostics
- invalid intrinsic use
- cannot map a type to Luau
- unsupported overload
- unsupported generic constraint
- unimplemented syntax node
- invalid Roblox API usage

Messages should be helpful, like:
- "CS2L0012: async/await is not supported yet."
- "CS2L0041: Could not lower invocation of method 'Foo.Bar<T>' to Luau."
- "CS2L0105: Roblox GetService<T>() requires a Roblox service type."
- "CS2L0106: Roblox GetService(string) should preferably use a string literal for better static typing."

Never silently do nonsense.

==================================================
AUTO-GENERATION OF ROBLOX API SHIM
==================================================

Design the code so the Roblox API shim can be partially generated from Roblox API metadata later.

For now, hand-implement a practical minimal set, but structure it so future work could consume an API dump and generate:
- C# type stubs
- inheritance structure
- properties/methods
- enums

Add TODO hooks or interfaces for this.

==================================================
TESTING REQUIREMENTS
==================================================

Add strong automated tests.

Include:
1. Intrinsic rewrite tests
   - generic GetService
   - string GetService
   - property globals
   - datatype constructors
   - signal Connect
   - enums

2. Snapshot tests
   - input C# -> expected Luau

3. Diagnostics tests
   - unsupported async
   - unsupported LINQ
   - invalid service generic
   - misuse of intrinsics

4. CLI tests
   - build command
   - output files
   - non-zero exit on errors

==================================================
EXAMPLE PROGRAMS
==================================================

Include examples such as:

1. BasicPartExample
   - create a Part
   - set properties
   - parent to workspace

2. ServiceExample
   - use:
       `game.GetService<Players>()`
       `game.GetService("ReplicatedStorage")`
       `game.ReplicatedStorage`

3. SignalExample
   - connect `Touched`

4. OOPExample
   - define a custom gameplay/service class in C#
   - compile to Luau class module

==================================================
IMPLEMENTATION NOTES
==================================================

- Use modern .NET and C#.
- Favor maintainable architecture over hacks.
- Write the code fully, not pseudo-code.
- Add comments where non-obvious.
- Include README with build/use instructions.
- Include sample config and example commands.
- Make best-effort choices when details are ambiguous.
- Where a feature is too large for V1, implement a proper diagnostic instead of leaving broken behavior.
- The project should compile successfully.

==================================================
SPECIFIC API DETAILS TO SUPPORT
==================================================

The Roblox shim must support both of these signatures:

    public T GetService<T>() where T : Instance
    public Instance GetService(string className)

Compiler behavior:
- Generic form emits:
    game:GetService("TypeName")
- String form emits:
    game:GetService("ProvidedString")

Also support:

    public static T New<T>() where T : Instance
    public static Instance New(string className)

Compiler behavior:
- `Instance.New<Part>()` -> `Instance.new("Part")`
- `Instance.New("Part")` -> `Instance.new("Part")`

==================================================
DELIVERABLES
==================================================

Generate the full codebase, including:
- solution and project files
- compiler library
- CLI
- Roblox shim package
- Luau runtime
- tests
- examples
- README
- sample config
- optional `.rbxmx` output support if feasible in this pass

Do not just describe the architecture. Actually write the project files and source code.

When uncertain, choose a sensible implementation and keep moving.