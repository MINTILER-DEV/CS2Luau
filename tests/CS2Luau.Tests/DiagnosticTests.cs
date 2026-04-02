namespace CS2Luau.Tests;

public sealed class DiagnosticTests
{
    [Fact]
    public async Task ReportsUnsupportedAsyncAwait()
    {
        var source =
            """
            using System.Threading.Tasks;

            public class Demo
            {
                public async Task RunAsync()
                {
                    await Task.Delay(1);
                }
            }
            """;

        var result = await CompilerHarness.CompileSourceAsync(source);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "CS2L0012");
    }

    [Fact]
    public async Task ReportsUnsupportedLinqSyntax()
    {
        var source =
            """
            using System.Linq;

            var query = from value in new[] { 1, 2, 3 } select value;
            """;

        var result = await CompilerHarness.CompileSourceAsync(source);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "CS2L0013");
    }

    [Fact]
    public async Task ReportsInvalidServiceGeneric()
    {
        var source =
            """
            using static Roblox.Globals;

            public class CustomInstance : Instance
            {
            }

            var value = game.GetService<CustomInstance>();
            """;

        var result = await CompilerHarness.CompileSourceAsync(source);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "CS2L0105");
    }

    [Fact]
    public async Task WarnsOnNonLiteralGetServiceString()
    {
        var source =
            """
            using static Roblox.Globals;

            var name = "Players";
            var value = game.GetService(name);
            """;

        var result = await CompilerHarness.CompileSourceAsync(source);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "CS2L0106");
    }
}
