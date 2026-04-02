using CS2Luau.Compiler;
using CS2Luau.Compiler.Configuration;
using CS2Luau.Compiler.Diagnostics;

namespace CS2Luau.Cli;

public static class CliApplication
{
    public static async Task<int> RunAsync(
        IReadOnlyList<string> args,
        TextWriter stdout,
        TextWriter stderr,
        CancellationToken cancellationToken)
    {
        if (args.Count == 0)
        {
            await stderr.WriteLineAsync(GetUsage()).ConfigureAwait(false);
            return 1;
        }

        var command = args[0];
        try
        {
            return command switch
            {
                "build" => await RunBuildAsync(args.Skip(1).ToArray(), stdout, stderr, cancellationToken).ConfigureAwait(false),
                "compile" => await RunCompileAsync(args.Skip(1).ToArray(), stdout, stderr, cancellationToken).ConfigureAwait(false),
                _ => await WriteUnknownCommandAsync(command, stderr).ConfigureAwait(false),
            };
        }
        catch (Exception exception)
        {
            await stderr.WriteLineAsync(exception.Message).ConfigureAwait(false);
            return 1;
        }
    }

    private static async Task<int> RunBuildAsync(string[] args, TextWriter stdout, TextWriter stderr, CancellationToken cancellationToken)
    {
        var options = ParseOptions(args);
        var projectPath = options.TryGetValue("project", out var explicitProject) ? explicitProject : Directory.GetCurrentDirectory();
        var input = await Cs2LuauProjectLoader.LoadCompileInputAsync(
            projectPath,
            overrideOutputDirectory: options.GetValueOrDefault("out"),
            emitRbxmx: options.ContainsKey("emit-rbxmx") ? true : null,
            includeRuntime: options.ContainsKey("include-runtime") ? true : null,
            warningsAsErrors: options.ContainsKey("warnings-as-errors") ? true : null,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (options.ContainsKey("watch"))
        {
            return await WatchAsync(input, stdout, stderr, cancellationToken).ConfigureAwait(false);
        }

        return await BuildOnceAsync(input, options.ContainsKey("verbose"), stdout, stderr, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<int> RunCompileAsync(string[] args, TextWriter stdout, TextWriter stderr, CancellationToken cancellationToken)
    {
        if (args.Length == 0)
        {
            await stderr.WriteLineAsync("compile requires an input .cs file.").ConfigureAwait(false);
            return 1;
        }

        var inputFile = args[0];
        var options = ParseOptions(args.Skip(1).ToArray());
        var input = Cs2LuauProjectLoader.CreateSingleFileInput(
            inputFile,
            outputDirectory: options.GetValueOrDefault("out"),
            scriptType: options.TryGetValue("script-type", out var scriptType)
                ? Enum.Parse<ScriptKind>(scriptType, ignoreCase: true)
                : ScriptKind.ModuleScript,
            emitRbxmx: options.ContainsKey("emit-rbxmx"),
            includeRuntime: options.ContainsKey("include-runtime"),
            warningsAsErrors: options.ContainsKey("warnings-as-errors"));

        return await BuildOnceAsync(input, options.ContainsKey("verbose"), stdout, stderr, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<int> WatchAsync(CompileInput input, TextWriter stdout, TextWriter stderr, CancellationToken cancellationToken)
    {
        using var watcher = new FileSystemWatcher(input.ProjectDirectory, "*.cs")
        {
            IncludeSubdirectories = true,
            EnableRaisingEvents = true,
        };

        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        FileSystemEventHandler handler = (_, _) => changed.TrySetResult();
        RenamedEventHandler renameHandler = (_, _) => changed.TrySetResult();
        watcher.Changed += handler;
        watcher.Created += handler;
        watcher.Deleted += handler;
        watcher.Renamed += renameHandler;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await BuildOnceAsync(input, verbose: true, stdout, stderr, cancellationToken).ConfigureAwait(false);
                await stdout.WriteLineAsync("Watching for changes...").ConfigureAwait(false);
                using var registration = cancellationToken.Register(() => changed.TrySetCanceled(cancellationToken));
                await changed.Task.ConfigureAwait(false);
                changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }
        catch (OperationCanceledException)
        {
        }

        return 0;
    }

    private static async Task<int> BuildOnceAsync(CompileInput input, bool verbose, TextWriter stdout, TextWriter stderr, CancellationToken cancellationToken)
    {
        var compiler = new Cs2LuauCompiler();
        var result = await compiler.CompileAsync(input, cancellationToken).ConfigureAwait(false);

        foreach (var diagnostic in result.Diagnostics.OrderBy(diagnostic => diagnostic.Location?.FilePath).ThenBy(diagnostic => diagnostic.Location?.Line))
        {
            var formatted = FormatDiagnostic(diagnostic);
            if (diagnostic.IsError)
            {
                await stderr.WriteLineAsync(formatted).ConfigureAwait(false);
            }
            else
            {
                await stdout.WriteLineAsync(formatted).ConfigureAwait(false);
            }
        }

        if (!result.Success)
        {
            return 1;
        }

        Directory.CreateDirectory(input.OutputDirectory);
        foreach (var output in result.Outputs)
        {
            var destinationPath = Path.Combine(input.OutputDirectory, output.RelativePath);
            var destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            await File.WriteAllTextAsync(destinationPath, output.Content, cancellationToken).ConfigureAwait(false);
            if (verbose)
            {
                await stdout.WriteLineAsync($"Wrote {destinationPath}").ConfigureAwait(false);
            }
        }

        return 0;
    }

    private static Dictionary<string, string> ParseOptions(string[] args)
    {
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            if (!argument.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            var key = argument[2..];
            if (index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                options[key] = args[index + 1];
                index++;
            }
            else
            {
                options[key] = "true";
            }
        }

        return options;
    }

    private static string FormatDiagnostic(CompilerDiagnostic diagnostic)
    {
        if (diagnostic.Location is null)
        {
            return $"{diagnostic.Severity.ToString().ToLowerInvariant()} {diagnostic.Code}: {diagnostic.Message}";
        }

        return $"{diagnostic.Location.FilePath}({diagnostic.Location.Line},{diagnostic.Location.Column}): {diagnostic.Severity.ToString().ToLowerInvariant()} {diagnostic.Code}: {diagnostic.Message}";
    }

    private static Task<int> WriteUnknownCommandAsync(string command, TextWriter stderr)
    {
        stderr.WriteLine(GetUsage() + Environment.NewLine + $"Unknown command '{command}'.");
        return Task.FromResult(1);
    }

    private static string GetUsage()
    {
        return """
            Usage:
              cs2luau build --project ./MyGame --out out --emit-rbxmx --include-runtime --watch --verbose
              cs2luau compile Program.cs --out out --emit-rbxmx --include-runtime --verbose
            """;
    }
}
