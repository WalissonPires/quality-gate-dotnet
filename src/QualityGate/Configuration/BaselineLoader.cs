using System.Text.Json;
using QualityGate.Domain;

namespace QualityGate.Configuration;

public static class BaselineLoader
{
    private const string DefaultRelativePath = ".tools/quality-gate/baseline.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static string ResolvePath(string? customPath, string workingDirectory)
    {
        if (!string.IsNullOrWhiteSpace(customPath))
        {
            return Path.IsPathRooted(customPath)
                ? customPath
                : Path.GetFullPath(Path.Combine(workingDirectory, customPath));
        }

        return Path.GetFullPath(Path.Combine(workingDirectory, DefaultRelativePath));
    }

    public static async Task<QualityBaseline?> LoadAsync(
        string? customPath,
        string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        var resolvedPath = ResolvePath(customPath, workingDirectory);
        if (!File.Exists(resolvedPath))
        {
            return null;
        }

        string jsonContent;
        try
        {
            jsonContent = await File.ReadAllTextAsync(resolvedPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new ConfigurationException($"Failed to read baseline file at '{resolvedPath}': {ex.Message}", ex);
        }

        return LoadFromJson(jsonContent, resolvedPath);
    }

    public static QualityBaseline LoadFromJson(string jsonContent, string sourceName = "baseline JSON")
    {
        if (string.IsNullOrWhiteSpace(jsonContent))
        {
            throw new ConfigurationException($"Baseline content from {sourceName} is empty.");
        }

        QualityBaseline baseline;
        try
        {
            baseline = JsonSerializer.Deserialize<QualityBaseline>(jsonContent, JsonOptions)
                       ?? throw new ConfigurationException($"Failed to deserialize baseline from {sourceName}.");
        }
        catch (JsonException ex)
        {
            throw new ConfigurationException($"Malformed JSON in baseline from {sourceName}: {ex.Message}", ex);
        }

        if (baseline.SchemaVersion != 1)
        {
            throw new ConfigurationException($"Unsupported baseline schema version {baseline.SchemaVersion} in {sourceName}. Expected version 1.");
        }

        return baseline;
    }

    public static async Task SaveAsync(
        QualityBaseline baseline,
        string? customPath,
        string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(baseline);

        var resolvedPath = ResolvePath(customPath, workingDirectory);
        var dir = Path.GetDirectoryName(resolvedPath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var json = JsonSerializer.Serialize(baseline, JsonOptions);
        await File.WriteAllTextAsync(resolvedPath, json, cancellationToken).ConfigureAwait(false);
    }
}
