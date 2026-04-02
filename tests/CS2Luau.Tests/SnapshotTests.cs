namespace CS2Luau.Tests;

public sealed class SnapshotTests
{
    [Fact]
    public async Task BasicPartExampleMatchesExpectedLuauSnapshot()
    {
        var source =
            """
            using static Roblox.Globals;

            var part = Instance.New<Part>();
            part.Name = "Hello";
            part.Anchored = true;
            part.Position = new Vector3(0, 10, 0);
            part.Parent = workspace;
            """;

        var result = await CompilerHarness.CompileSourceAsync(source);

        var expected =
            """
            local exports = {}

            local part = Instance.new("Part")
            part.Name = "Hello"
            part.Anchored = true
            part.Position = Vector3.new(0, 10, 0)
            part.Parent = workspace

            return exports
            """.ReplaceLineEndings();

        Assert.True(result.Success);
        var luau = result.Outputs.Single(output => output.Kind == CS2Luau.Compiler.Configuration.GeneratedOutputKind.Luau).Content.ReplaceLineEndings();
        Assert.Equal(expected + Environment.NewLine, luau);
    }
}
