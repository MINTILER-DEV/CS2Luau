using CS2Luau.Compiler.Configuration;

namespace CS2Luau.Tests;

public sealed class IntrinsicRewriteTests
{
    [Fact]
    public async Task RewritesGenericAndStringGetServiceCalls()
    {
        var source =
            """
            using static Roblox.Globals;

            var players = game.GetService<Players>();
            var storage = game.GetService("ReplicatedStorage");
            var direct = game.ReplicatedStorage;
            """;

        var result = await CompilerHarness.CompileSourceAsync(source);

        Assert.True(result.Success);
        var luau = result.Outputs.Single(output => output.Kind == GeneratedOutputKind.Luau).Content;
        Assert.Contains("local players = game:GetService(\"Players\")", luau);
        Assert.Contains("local storage = game:GetService(\"ReplicatedStorage\")", luau);
        Assert.Contains("local direct = game.ReplicatedStorage", luau);
    }

    [Fact]
    public async Task RewritesInstanceCreationDatatypesSignalsAndEnums()
    {
        var source =
            """
            using static Roblox.Globals;

            var part = Instance.New<Part>();
            part.Material = Material.Neon;
            part.Position = new Vector3(0, 5, 0);
            part.Touched.Connect(hit => {
                print(hit.Name);
            });
            """;

        var result = await CompilerHarness.CompileSourceAsync(source);

        Assert.True(result.Success);
        var luau = result.Outputs.Single(output => output.Kind == GeneratedOutputKind.Luau).Content;
        Assert.Contains("local part = Instance.new(\"Part\")", luau);
        Assert.Contains("part.Material = Enum.Material.Neon", luau);
        Assert.Contains("part.Position = Vector3.new(0, 5, 0)", luau);
        Assert.Contains("part.Touched:Connect(function(hit)", luau);
        Assert.Contains("print(hit.Name)", luau);
    }

    [Fact]
    public async Task RewritesRobloxStyleEnumWrapperSyntax()
    {
        var source =
            """
            using static Roblox.Globals;

            var part = Instance.New<Part>();
            part.Material = Enum.Material.Neon;
            """;

        var result = await CompilerHarness.CompileSourceAsync(source);

        Assert.True(result.Success);
        var luau = result.Outputs.Single(output => output.Kind == GeneratedOutputKind.Luau).Content;
        Assert.Contains("part.Material = Enum.Material.Neon", luau);
    }
}
