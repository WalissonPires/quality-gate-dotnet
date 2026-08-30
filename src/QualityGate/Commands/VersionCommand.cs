using System.CommandLine;
using System.Reflection;

namespace QualityGate.Commands;

public static class VersionCommand
{
    public static Command Create()
    {
        var command = new Command("version", "Print QualityGate version");

        command.SetHandler(() =>
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
            Console.WriteLine($"QualityGate {version}");
        });

        return command;
    }
}
