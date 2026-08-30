namespace QualityGate.Domain;

public sealed record QualityTarget
{
    public QualityScope Scope { get; }
    public string? Project { get; }
    public string? Namespace { get; }

    public QualityTarget(QualityScope scope, string? project = null, string? @namespace = null)
    {
        if (scope == QualityScope.Project && string.IsNullOrWhiteSpace(project))
        {
            throw new ArgumentException("Project path is required when scope is Project.", nameof(project));
        }

        if (scope == QualityScope.Namespace && string.IsNullOrWhiteSpace(@namespace))
        {
            throw new ArgumentException("Namespace is required when scope is Namespace.", nameof(@namespace));
        }

        if (scope is QualityScope.Diff or QualityScope.Repository)
        {
            if (!string.IsNullOrWhiteSpace(project) || !string.IsNullOrWhiteSpace(@namespace))
            {
                throw new ArgumentException($"Project and Namespace must be null when scope is {scope}.");
            }
        }

        Scope = scope;
        Project = project;
        Namespace = @namespace;
    }

    public static QualityTarget ForDiff() => new(QualityScope.Diff);
    public static QualityTarget ForRepository() => new(QualityScope.Repository);
    public static QualityTarget ForProject(string project) => new(QualityScope.Project, project: project);
    public static QualityTarget ForNamespace(string @namespace) => new(QualityScope.Namespace, @namespace: @namespace);
}
