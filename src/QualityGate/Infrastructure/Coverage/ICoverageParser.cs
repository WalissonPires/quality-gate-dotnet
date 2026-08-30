using QualityGate.Domain;

namespace QualityGate.Infrastructure.Coverage;

public sealed record LineCoverageInfo(int LineNumber, int Hits, bool IsBranch = false, string? ConditionCoverage = null)
{
    public bool IsCovered => Hits > 0;
}

public sealed record FileCoverageReport(
    string FilePath,
    int LinesValid,
    int LinesCovered,
    int BranchesValid,
    int BranchesCovered,
    IReadOnlyDictionary<int, LineCoverageInfo> Lines)
{
    public decimal LineRate => LinesValid > 0 ? (decimal)LinesCovered / LinesValid * 100m : 100m;
    public decimal BranchRate => BranchesValid > 0 ? (decimal)BranchesCovered / BranchesValid * 100m : 100m;
}

public sealed record CoverageSummary(
    int TotalLines,
    int CoveredLines,
    int TotalBranches,
    int CoveredBranches,
    IReadOnlyDictionary<string, FileCoverageReport> FileReports)
{
    public decimal LineRate => TotalLines > 0 ? (decimal)CoveredLines / TotalLines * 100m : 100m;
    public decimal BranchRate => TotalBranches > 0 ? (decimal)CoveredBranches / TotalBranches * 100m : 100m;
}

public interface ICoverageParser
{
    Task<CoverageSummary> ParseAsync(string coberturaXmlPath, CancellationToken cancellationToken = default);
    Task<CoverageSummary> ParseMultipleAsync(IEnumerable<string> coberturaXmlPaths, CancellationToken cancellationToken = default);
}
