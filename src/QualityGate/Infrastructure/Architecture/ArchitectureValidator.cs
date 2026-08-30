using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using QualityGate.Configuration;
using QualityGate.Domain;

namespace QualityGate.Infrastructure.Architecture;

public sealed record ArchitectureViolation(
    string RuleName,
    string FilePath,
    int LineNumber,
    string SourceNamespace,
    string ForbiddenDependency,
    string Message);

public interface IArchitectureValidator
{
    Task<IReadOnlyList<ArchitectureViolation>> ValidateAsync(
        IEnumerable<string> sourceFilePaths,
        IEnumerable<ArchitectureRuleOptions> rules,
        CancellationToken cancellationToken = default);
}

public sealed class ArchitectureValidator : IArchitectureValidator
{
    public async Task<IReadOnlyList<ArchitectureViolation>> ValidateAsync(
        IEnumerable<string> sourceFilePaths,
        IEnumerable<ArchitectureRuleOptions> rules,
        CancellationToken cancellationToken = default)
    {
        var ruleList = rules.ToList();
        if (ruleList.Count == 0) return [];

        var violations = new List<ArchitectureViolation>();

        foreach (var filePath in sourceFilePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!File.Exists(filePath) || !filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string code;
            try
            {
                code = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                continue;
            }

            var tree = CSharpSyntaxTree.ParseText(code, cancellationToken: cancellationToken);
            var root = await tree.GetRootAsync(cancellationToken).ConfigureAwait(false);

            // Extract namespace declarations
            var fileNamespaces = root.DescendantNodes()
                .OfType<BaseNamespaceDeclarationSyntax>()
                .Select(n => n.Name.ToString())
                .ToList();

            if (fileNamespaces.Count == 0)
            {
                // Fallback: derive namespace from file path if file-scoped namespace or simple file
                var normalized = filePath.Replace('\\', '/');
                var parts = normalized.Split('/');
                var namespaceCandidate = string.Join('.', parts.Take(parts.Length - 1));
                fileNamespaces.Add(namespaceCandidate);
            }

            // Extract using directives
            var usings = root.DescendantNodes()
                .OfType<UsingDirectiveSyntax>()
                .Where(u => u.Name != null)
                .Select(u => new { Name = u.Name!.ToString(), Line = u.GetLocation().GetLineSpan().StartLinePosition.Line + 1 })
                .ToList();
            foreach (var rule in ruleList)
            {
                foreach (var ns in fileNamespaces)
                {
                    if (!MatchesPattern(ns, rule.Source))
                    {
                        continue;
                    }

                    foreach (var u in usings)
                    {
                        foreach (var forbidden in rule.ForbiddenDependencies)
                        {
                            if (MatchesPattern(u.Name, forbidden))
                            {
                                violations.Add(new ArchitectureViolation(
                                    rule.Name,
                                    filePath.Replace('\\', '/'),
                                    u.Line,
                                    ns,
                                    u.Name,
                                    $"Architecture rule '{rule.Name}' violated: Namespace '{ns}' cannot reference forbidden dependency '{u.Name}'."));
                            }
                        }
                    }
                }
            }
        }

        return violations;
    }

    public static bool MatchesPattern(string actual, string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern) || string.IsNullOrWhiteSpace(actual))
        {
            return false;
        }

        if (pattern == "*" || pattern == actual)
        {
            return true;
        }

        if (!pattern.Contains('*'))
        {
            return actual.Equals(pattern, StringComparison.OrdinalIgnoreCase) ||
                   actual.StartsWith(pattern + ".", StringComparison.OrdinalIgnoreCase);
        }

        var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\*", ".*") + ".*$";
        return System.Text.RegularExpressions.Regex.IsMatch(actual, regexPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
}
