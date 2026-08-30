using System.Reflection;

namespace QualityGate.Domain;

public static class ToolVersion
{
    private static readonly Lazy<string> CurrentVersion = new(ResolveVersion);

    public static string Current => CurrentVersion.Value;

    private static string ResolveVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();

        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            var plusIndex = informationalVersion.IndexOf('+');
            var versionWithoutCommit = plusIndex > 0 ? informationalVersion[..plusIndex] : informationalVersion;
            if (!string.IsNullOrWhiteSpace(versionWithoutCommit))
            {
                return versionWithoutCommit;
            }
        }

        var assemblyVersion = assembly.GetName().Version;
        if (assemblyVersion != null)
        {
            return assemblyVersion.ToString(3);
        }

        return "1.0.0";
    }
}
