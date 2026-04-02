namespace CS2Luau.Cli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        return await CliApplication.RunAsync(args, Console.Out, Console.Error, CancellationToken.None).ConfigureAwait(false);
    }
}
