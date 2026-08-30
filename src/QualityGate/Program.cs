using System.CommandLine;
using QualityGate.Commands;

namespace QualityGate;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Quality Gate CLI - automated code quality pipeline for Wamage")
        {
            CheckCommand.Create(),
            VersionCommand.Create(),
            BaselineCommand.Create()
        };

        try
        {
            return await rootCommand.InvokeAsync(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal internal error: {ex}");
            return 5;
        }
    }
}
