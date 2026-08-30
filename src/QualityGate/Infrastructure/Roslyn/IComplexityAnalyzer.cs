namespace QualityGate.Infrastructure.Roslyn;

public sealed record MethodComplexity(
    string MethodName,
    string ClassName,
    string FilePath,
    int LineNumber,
    int CyclomaticComplexity,
    int LineCount);

public sealed record ClassComplexity(
    string ClassName,
    string FilePath,
    int LineNumber,
    int LineCount);

public sealed record ComplexityAnalysisResult(
    IReadOnlyList<MethodComplexity> Methods,
    IReadOnlyList<ClassComplexity> Classes)
{
    public int MaxCyclomaticComplexity => Methods.Count > 0 ? Methods.Max(m => m.CyclomaticComplexity) : 1;
    public int MaxMethodLines => Methods.Count > 0 ? Methods.Max(m => m.LineCount) : 0;
    public int MaxClassLines => Classes.Count > 0 ? Classes.Max(c => c.LineCount) : 0;
}

public interface IComplexityAnalyzer
{
    Task<ComplexityAnalysisResult> AnalyzeFilesAsync(IEnumerable<string> filePaths, CancellationToken cancellationToken = default);
    Task<ComplexityAnalysisResult> AnalyzeSourceAsync(string sourceCode, string filePath = "source.cs", CancellationToken cancellationToken = default);
}
