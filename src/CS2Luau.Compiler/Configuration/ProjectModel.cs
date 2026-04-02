using System.Text.Json;
using System.Text.Json.Serialization;

namespace CS2Luau.Compiler.Configuration;

public enum ScriptKind
{
    ModuleScript,
    Script,
    LocalScript,
}

public sealed class Cs2LuauProject
{
    public string Name { get; init; } = "Game";
    public string SourceDir { get; init; } = "src";
    public string OutDir { get; init; } = "out";
    public bool EmitRbxmx { get; init; }
    public bool IncludeRuntime { get; init; } = true;
    public string RootNamespace { get; init; } = string.Empty;
    public IReadOnlyList<string> Entrypoints { get; init; } = [];
    public ScriptKind ScriptType { get; init; } = ScriptKind.ModuleScript;
    public string RobloxApiMode { get; init; } = "Manual";
    public bool WarningsAsErrors { get; init; }
}

public sealed record SourceDocument(string Path, string Text);

public sealed class CompileInput
{
    public required string Name { get; init; }
    public required string ProjectDirectory { get; init; }
    public required string OutputDirectory { get; init; }
    public required IReadOnlyList<SourceDocument> Documents { get; init; }
    public ScriptKind ScriptType { get; init; } = ScriptKind.ModuleScript;
    public bool EmitRbxmx { get; init; }
    public bool IncludeRuntime { get; init; } = true;
    public bool WarningsAsErrors { get; init; }
}

public enum GeneratedOutputKind
{
    Luau,
    Runtime,
    Rbxmx,
}

public sealed record GeneratedOutput(string RelativePath, string Content, GeneratedOutputKind Kind);

public sealed class CompileResult
{
    public required IReadOnlyList<GeneratedOutput> Outputs { get; init; }
    public required IReadOnlyList<Diagnostics.CompilerDiagnostic> Diagnostics { get; init; }

    public bool Success => Diagnostics.All(d => !d.IsError);
}

public static class Cs2LuauProjectLoader
{
    public static async Task<Cs2LuauProject> LoadAsync(string projectPath, CancellationToken cancellationToken = default)
    {
        var resolvedPath = Directory.Exists(projectPath)
            ? Path.Combine(projectPath, "cs2luau.json")
            : projectPath;

        if (!File.Exists(resolvedPath))
        {
            throw new FileNotFoundException("Could not find cs2luau.json.", resolvedPath);
        }

        var json = await File.ReadAllTextAsync(resolvedPath, cancellationToken).ConfigureAwait(false);
        var project = JsonSerializer.Deserialize<Cs2LuauProject>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            Converters = { new JsonStringEnumConverter() },
        });

        return project ?? throw new InvalidOperationException("Could not deserialize cs2luau.json.");
    }

    public static async Task<CompileInput> LoadCompileInputAsync(
        string projectPath,
        string? overrideOutputDirectory = null,
        bool? emitRbxmx = null,
        bool? includeRuntime = null,
        bool? warningsAsErrors = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedProjectPath = Directory.Exists(projectPath)
            ? Path.Combine(projectPath, "cs2luau.json")
            : projectPath;
        var projectDirectory = Path.GetDirectoryName(Path.GetFullPath(resolvedProjectPath))
            ?? throw new InvalidOperationException("Could not determine the project directory.");
        var project = await LoadAsync(resolvedProjectPath, cancellationToken).ConfigureAwait(false);
        var sourceDirectory = Path.GetFullPath(Path.Combine(projectDirectory, project.SourceDir));

        if (!Directory.Exists(sourceDirectory))
        {
            throw new DirectoryNotFoundException($"Could not find source directory '{sourceDirectory}'.");
        }

        var documents = Directory.EnumerateFiles(sourceDirectory, "*.cs", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => new SourceDocument(path, File.ReadAllText(path)))
            .ToArray();

        return new CompileInput
        {
            Name = project.Name,
            ProjectDirectory = projectDirectory,
            OutputDirectory = Path.GetFullPath(Path.Combine(projectDirectory, overrideOutputDirectory ?? project.OutDir)),
            Documents = documents,
            EmitRbxmx = emitRbxmx ?? project.EmitRbxmx,
            IncludeRuntime = includeRuntime ?? project.IncludeRuntime,
            ScriptType = project.ScriptType,
            WarningsAsErrors = warningsAsErrors ?? project.WarningsAsErrors,
        };
    }

    public static CompileInput CreateSingleFileInput(
        string filePath,
        string? outputDirectory = null,
        ScriptKind scriptType = ScriptKind.ModuleScript,
        bool emitRbxmx = false,
        bool includeRuntime = true,
        bool warningsAsErrors = false)
    {
        var resolvedFilePath = Path.GetFullPath(filePath);
        if (!File.Exists(resolvedFilePath))
        {
            throw new FileNotFoundException("Could not find the input C# file.", resolvedFilePath);
        }

        var projectDirectory = Path.GetDirectoryName(resolvedFilePath)
            ?? throw new InvalidOperationException("Could not determine the input directory.");

        return new CompileInput
        {
            Name = Path.GetFileNameWithoutExtension(resolvedFilePath),
            ProjectDirectory = projectDirectory,
            OutputDirectory = Path.GetFullPath(outputDirectory ?? Path.Combine(projectDirectory, "out")),
            Documents = [new SourceDocument(resolvedFilePath, File.ReadAllText(resolvedFilePath))],
            EmitRbxmx = emitRbxmx,
            IncludeRuntime = includeRuntime,
            ScriptType = scriptType,
            WarningsAsErrors = warningsAsErrors,
        };
    }
}
