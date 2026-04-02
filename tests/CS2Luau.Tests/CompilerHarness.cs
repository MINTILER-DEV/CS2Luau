using CS2Luau.Compiler;
using CS2Luau.Compiler.Configuration;

namespace CS2Luau.Tests;

internal static class CompilerHarness
{
    public static async Task<CompileResult> CompileSourceAsync(
        string source,
        ScriptKind scriptKind = ScriptKind.ModuleScript,
        bool emitRbxmx = false,
        bool includeRuntime = false)
    {
        var root = Path.Combine(Path.GetTempPath(), "cs2luau-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var sourcePath = Path.Combine(root, "Program.cs");
        await File.WriteAllTextAsync(sourcePath, source).ConfigureAwait(false);

        var input = Cs2LuauProjectLoader.CreateSingleFileInput(
            sourcePath,
            outputDirectory: Path.Combine(root, "out"),
            scriptType: scriptKind,
            emitRbxmx: emitRbxmx,
            includeRuntime: includeRuntime);

        var compiler = new Cs2LuauCompiler();
        return await compiler.CompileAsync(input).ConfigureAwait(false);
    }
}
