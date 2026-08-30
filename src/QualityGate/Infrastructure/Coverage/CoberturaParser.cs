using System.Xml.Linq;

namespace QualityGate.Infrastructure.Coverage;

public sealed class CoberturaParser : ICoverageParser
{
    public async Task<CoverageSummary> ParseAsync(string coberturaXmlPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(coberturaXmlPath) || !File.Exists(coberturaXmlPath))
        {
            return new CoverageSummary(0, 0, 0, 0, new Dictionary<string, FileCoverageReport>());
        }

        string xmlContent;
        try
        {
            xmlContent = await File.ReadAllTextAsync(coberturaXmlPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new CoverageSummary(0, 0, 0, 0, new Dictionary<string, FileCoverageReport>());
        }

        return ParseXml(xmlContent);
    }

    public async Task<CoverageSummary> ParseMultipleAsync(IEnumerable<string> coberturaXmlPaths, CancellationToken cancellationToken = default)
    {
        var reports = new List<CoverageSummary>();
        foreach (var path in coberturaXmlPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var summary = await ParseAsync(path, cancellationToken).ConfigureAwait(false);
            reports.Add(summary);
        }

        return MergeSummaries(reports);
    }

    public static CoverageSummary ParseXml(string xmlContent)
    {
        if (string.IsNullOrWhiteSpace(xmlContent))
        {
            return new CoverageSummary(0, 0, 0, 0, new Dictionary<string, FileCoverageReport>());
        }

        var doc = XDocument.Parse(xmlContent);
        var fileReports = new Dictionary<string, FileCoverageReport>(StringComparer.OrdinalIgnoreCase);

        var classes = doc.Descendants("class");
        foreach (var cls in classes)
        {
            var filename = cls.Attribute("filename")?.Value;
            if (string.IsNullOrWhiteSpace(filename)) continue;

            var normalizedPath = filename.Replace('\\', '/');

            var lineDict = new Dictionary<int, LineCoverageInfo>();
            int linesValid = 0;
            int linesCovered = 0;
            int branchesValid = 0;
            int branchesCovered = 0;

            var lines = cls.Descendants("line");
            foreach (var lineElem in lines)
            {
                var numAttr = lineElem.Attribute("number")?.Value;
                var hitsAttr = lineElem.Attribute("hits")?.Value;
                var branchAttr = lineElem.Attribute("branch")?.Value;
                var conditionCoverage = lineElem.Attribute("condition-coverage")?.Value;

                if (!int.TryParse(numAttr, out var lineNum) || !int.TryParse(hitsAttr, out var hits))
                {
                    continue;
                }

                bool isBranch = bool.TryParse(branchAttr, out var b) && b;

                linesValid++;
                if (hits > 0) linesCovered++;

                if (isBranch)
                {
                    branchesValid++;
                    if (hits > 0) branchesCovered++;
                }

                lineDict[lineNum] = new LineCoverageInfo(lineNum, hits, isBranch, conditionCoverage);
            }

            if (fileReports.TryGetValue(normalizedPath, out var existing))
            {
                // Merge classes with same filename
                var mergedLines = new Dictionary<int, LineCoverageInfo>(existing.Lines);
                foreach (var (lineNum, info) in lineDict)
                {
                    if (mergedLines.TryGetValue(lineNum, out var existingLine))
                    {
                        mergedLines[lineNum] = new LineCoverageInfo(
                            lineNum,
                            existingLine.Hits + info.Hits,
                            existingLine.IsBranch || info.IsBranch,
                            info.ConditionCoverage ?? existingLine.ConditionCoverage);
                    }
                    else
                    {
                        mergedLines[lineNum] = info;
                    }
                }

                int mergedValid = mergedLines.Count;
                int mergedCovered = mergedLines.Values.Count(l => l.IsCovered);
                int mergedBranchesValid = mergedLines.Values.Count(l => l.IsBranch);
                int mergedBranchesCovered = mergedLines.Values.Count(l => l.IsBranch && l.IsCovered);

                fileReports[normalizedPath] = new FileCoverageReport(
                    normalizedPath,
                    mergedValid,
                    mergedCovered,
                    mergedBranchesValid,
                    mergedBranchesCovered,
                    mergedLines);
            }
            else
            {
                fileReports[normalizedPath] = new FileCoverageReport(
                    normalizedPath,
                    linesValid,
                    linesCovered,
                    branchesValid,
                    branchesCovered,
                    lineDict);
            }
        }

        int totalValidLines = fileReports.Values.Sum(f => f.LinesValid);
        int totalCoveredLines = fileReports.Values.Sum(f => f.LinesCovered);
        int totalValidBranches = fileReports.Values.Sum(f => f.BranchesValid);
        int totalCoveredBranches = fileReports.Values.Sum(f => f.BranchesCovered);

        return new CoverageSummary(totalValidLines, totalCoveredLines, totalValidBranches, totalCoveredBranches, fileReports);
    }

    public static CoverageSummary MergeSummaries(IEnumerable<CoverageSummary> summaries)
    {
        var mergedFiles = new Dictionary<string, FileCoverageReport>(StringComparer.OrdinalIgnoreCase);

        foreach (var summary in summaries)
        {
            foreach (var (filePath, report) in summary.FileReports)
            {
                if (mergedFiles.TryGetValue(filePath, out var existing))
                {
                    var mergedLines = new Dictionary<int, LineCoverageInfo>(existing.Lines);
                    foreach (var (lineNum, info) in report.Lines)
                    {
                        if (mergedLines.TryGetValue(lineNum, out var existingLine))
                        {
                            mergedLines[lineNum] = new LineCoverageInfo(
                                lineNum,
                                existingLine.Hits + info.Hits,
                                existingLine.IsBranch || info.IsBranch,
                                info.ConditionCoverage ?? existingLine.ConditionCoverage);
                        }
                        else
                        {
                            mergedLines[lineNum] = info;
                        }
                    }

                    int valid = mergedLines.Count;
                    int covered = mergedLines.Values.Count(l => l.IsCovered);
                    int bValid = mergedLines.Values.Count(l => l.IsBranch);
                    int bCovered = mergedLines.Values.Count(l => l.IsBranch && l.IsCovered);

                    mergedFiles[filePath] = new FileCoverageReport(filePath, valid, covered, bValid, bCovered, mergedLines);
                }
                else
                {
                    mergedFiles[filePath] = report;
                }
            }
        }

        int totalLines = mergedFiles.Values.Sum(f => f.LinesValid);
        int coveredLines = mergedFiles.Values.Sum(f => f.LinesCovered);
        int totalBranches = mergedFiles.Values.Sum(f => f.BranchesValid);
        int coveredBranches = mergedFiles.Values.Sum(f => f.BranchesCovered);

        return new CoverageSummary(totalLines, coveredLines, totalBranches, coveredBranches, mergedFiles);
    }
}
