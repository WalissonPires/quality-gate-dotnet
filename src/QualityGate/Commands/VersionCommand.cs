using System.CommandLine;
using QualityGate.Domain;

namespace QualityGate.Commands;

public static class VersionCommand
{
    public static Command Create()
    {
        var command = new Command("version", "Print QualityGate version");

        command.SetHandler(() =>
        {
            var version = ToolVersion.Current;
            Console.WriteLine($"QualityGate {version}");
        });

        return command;
    }
}
