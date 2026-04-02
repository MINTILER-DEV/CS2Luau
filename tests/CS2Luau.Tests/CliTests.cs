using CS2Luau.Cli;

namespace CS2Luau.Tests;

public sealed class CliTests
{
    [Fact]
    public async Task BuildCommandWritesOutputs()
    {
        var root = Path.Combine(Path.GetTempPath(), "cs2luau-cli", Guid.NewGuid().ToString("N"));
        var sourceDir = Path.Combine(root, "src");
        Directory.CreateDirectory(sourceDir);

        await File.WriteAllTextAsync(Path.Combine(root, "cs2luau.json"),
            """
            {
              "name": "CliExample",
              "sourceDir": "src",
              "outDir": "out",
              "emitRbxmx": true,
              "includeRuntime": true,
              "scriptType": "ModuleScript"
            }
            """);

        await File.WriteAllTextAsync(Path.Combine(sourceDir, "Program.cs"),
            """
            using static Roblox.Globals;

            var part = Instance.New<Part>();
            part.Parent = workspace;
            """);

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var exitCode = await CliApplication.RunAsync(["build", "--project", root, "--verbose"], stdout, stderr, CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(Path.Combine(root, "out", "CliExample.luau")));
        Assert.True(File.Exists(Path.Combine(root, "out", "CliExample.rbxmx")));
        Assert.True(File.Exists(Path.Combine(root, "out", "runtime", "Class.luau")));
    }

    [Fact]
    public async Task BuildReturnsNonZeroOnCompilerErrors()
    {
        var root = Path.Combine(Path.GetTempPath(), "cs2luau-cli", Guid.NewGuid().ToString("N"));
        var sourceDir = Path.Combine(root, "src");
        Directory.CreateDirectory(sourceDir);

        await File.WriteAllTextAsync(Path.Combine(root, "cs2luau.json"),
            """
            {
              "name": "BrokenExample",
              "sourceDir": "src",
              "outDir": "out",
              "scriptType": "ModuleScript"
            }
            """);

        await File.WriteAllTextAsync(Path.Combine(sourceDir, "Program.cs"),
            """
            using System.Threading.Tasks;

            public class Demo
            {
                public async Task RunAsync()
                {
                    await Task.Delay(1);
                }
            }
            """);

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var exitCode = await CliApplication.RunAsync(["build", "--project", root], stdout, stderr, CancellationToken.None);

        Assert.Equal(1, exitCode);
        Assert.Contains("CS2L0012", stderr.ToString() + stdout);
    }
}
