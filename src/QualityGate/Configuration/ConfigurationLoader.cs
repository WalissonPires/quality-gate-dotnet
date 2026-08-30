using System.Text.Json;

namespace QualityGate.Configuration;

public static class ConfigurationLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public static async Task<QualityGateOptions> LoadAsync(string configPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(configPath))
        {
            throw new ConfigurationException("Configuration file path cannot be null or empty.");
        }

        if (!File.Exists(configPath))
        {
            throw new ConfigurationException($"Configuration file not found at: '{configPath}'");
        }

        string jsonContent;
        try
        {
            jsonContent = await File.ReadAllTextAsync(configPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new ConfigurationException($"Failed to read configuration file at '{configPath}': {ex.Message}", ex);
        }

        return LoadFromJson(jsonContent, configPath);
    }

    public static QualityGateOptions LoadFromJson(string jsonContent, string sourceName = "JSON string")
    {
        if (string.IsNullOrWhiteSpace(jsonContent))
        {
            throw new ConfigurationException($"Configuration content from {sourceName} is empty.");
        }

        QualityGateOptions options;
        try
        {
            options = JsonSerializer.Deserialize<QualityGateOptions>(jsonContent, JsonOptions)
                      ?? throw new ConfigurationException($"Failed to deserialize configuration from {sourceName}.");
        }
        catch (JsonException ex)
        {
            throw new ConfigurationException($"Malformed JSON in configuration from {sourceName}: {ex.Message}", ex);
        }

        Validate(options, sourceName);
        return options;
    }

    private static void Validate(QualityGateOptions options, string sourceName)
    {
        if (options.Version != 1)
        {
            throw new ConfigurationException($"Unsupported configuration schema version {options.Version} in {sourceName}. Supported version is 1.");
        }

        if (options.ChangedCode != null)
        {
            ValidatePercentage(options.ChangedCode.LineCoverage, "changedCode.lineCoverage", sourceName);
            ValidatePercentage(options.ChangedCode.BranchCoverage, "changedCode.branchCoverage", sourceName);
            ValidateNonNegative(options.ChangedCode.MaxCyclomaticComplexity, "changedCode.maxCyclomaticComplexity", sourceName);
            ValidatePositive(options.ChangedCode.MaxMethodLines, "changedCode.maxMethodLines", sourceName);
            ValidatePositive(options.ChangedCode.MaxClassLines, "changedCode.maxClassLines", sourceName);
            ValidateNonNegative(options.ChangedCode.NewWarnings, "changedCode.newWarnings", sourceName);
        }

        if (options.Global != null)
        {
            ValidatePercentage(options.Global.LineCoverage, "global.lineCoverage", sourceName);
            ValidatePercentage(options.Global.BranchCoverage, "global.branchCoverage", sourceName);
        }

        if (options.Mutation != null)
        {
            ValidatePercentage(options.Mutation.MinimumScore, "mutation.minimumScore", sourceName);
        }

        if (options.Execution != null)
        {
            ValidatePositive(options.Execution.ProcessTimeoutSeconds, "execution.processTimeoutSeconds", sourceName);
        }
    }

    private static void ValidatePercentage(decimal? value, string propertyName, string sourceName)
    {
        if (value.HasValue && (value.Value < 0 || value.Value > 100))
        {
            throw new ConfigurationException($"Property '{propertyName}' in {sourceName} must be between 0 and 100. Actual value: {value.Value}");
        }
    }

    private static void ValidateNonNegative(int? value, string propertyName, string sourceName)
    {
        if (value.HasValue && value.Value < 0)
        {
            throw new ConfigurationException($"Property '{propertyName}' in {sourceName} cannot be negative. Actual value: {value.Value}");
        }
    }

    private static void ValidatePositive(int? value, string propertyName, string sourceName)
    {
        if (value.HasValue && value.Value <= 0)
        {
            throw new ConfigurationException($"Property '{propertyName}' in {sourceName} must be greater than zero. Actual value: {value.Value}");
        }
    }
}
