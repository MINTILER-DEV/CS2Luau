using System.Text;

namespace CS2Luau.Roblox.Generator;

internal static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            Console.OutputEncoding = Encoding.UTF8;
            var options = GeneratorOptions.Parse(args);
            var dump = ApiDumpReader.Read(options.ApiDumpPath);
            RobloxCodeGenerator.Generate(dump, options.OutputDirectory);
            Console.WriteLine($"Generated Roblox shim files in '{options.OutputDirectory}'.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }
}

internal sealed record GeneratorOptions(string ApiDumpPath, string OutputDirectory)
{
    public static GeneratorOptions Parse(IReadOnlyList<string> args)
    {
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < args.Count; index++)
        {
            var current = args[index];
            if (!current.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            var key = current[2..];
            if (index + 1 >= args.Count || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Missing value for '{current}'.");
            }

            options[key] = args[index + 1];
            index++;
        }

        var apiDumpPath = Path.GetFullPath(options.GetValueOrDefault("api-dump") ?? Path.Combine("ignore", "apidump.json"));
        var outputDirectory = Path.GetFullPath(options.GetValueOrDefault("output-dir") ?? Path.Combine("src", "CS2Luau.Roblox", "Generated"));
        return new GeneratorOptions(apiDumpPath, outputDirectory);
    }
}
