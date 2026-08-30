using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace QualityGate.Infrastructure.Roslyn;

public sealed class ComplexityAnalyzer : IComplexityAnalyzer
{
    public async Task<ComplexityAnalysisResult> AnalyzeFilesAsync(IEnumerable<string> filePaths, CancellationToken cancellationToken = default)
    {
        var allMethods = new List<MethodComplexity>();
        var allClasses = new List<ClassComplexity>();

        foreach (var filePath in filePaths)
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

            var result = await AnalyzeSourceAsync(code, filePath, cancellationToken).ConfigureAwait(false);
            allMethods.AddRange(result.Methods);
            allClasses.AddRange(result.Classes);
        }

        return new ComplexityAnalysisResult(allMethods, allClasses);
    }

    public Task<ComplexityAnalysisResult> AnalyzeSourceAsync(string sourceCode, string filePath = "source.cs", CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            return Task.FromResult(new ComplexityAnalysisResult([], []));
        }

        var normalizedPath = filePath.Replace('\\', '/');
        var tree = CSharpSyntaxTree.ParseText(sourceCode, cancellationToken: cancellationToken);
        var root = tree.GetRoot(cancellationToken);

        var methods = new List<MethodComplexity>();
        var classes = new List<ClassComplexity>();

        // Analyze classes and records
        var typeDeclarations = root.DescendantNodes().OfType<TypeDeclarationSyntax>();
        foreach (var typeDecl in typeDeclarations)
        {
            var lineSpan = typeDecl.GetLocation().GetLineSpan();
            int lineCount = lineSpan.EndLinePosition.Line - lineSpan.StartLinePosition.Line + 1;
            int startLine = lineSpan.StartLinePosition.Line + 1;

            classes.Add(new ClassComplexity(typeDecl.Identifier.Text, normalizedPath, startLine, lineCount));
        }

        // Analyze methods, constructors, local functions
        var methodDeclarations = root.DescendantNodes().OfType<BaseMethodDeclarationSyntax>();
        foreach (var method in methodDeclarations)
        {
            var containingClass = method.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault()?.Identifier.Text ?? "Global";
            string methodName;
            if (method is MethodDeclarationSyntax m)
            {
                methodName = m.Identifier.Text;
            }
            else if (method is ConstructorDeclarationSyntax c)
            {
                methodName = c.Identifier.Text;
            }
            else
            {
                methodName = method.ToString();
            }

            var lineSpan = method.GetLocation().GetLineSpan();
            int lineCount = lineSpan.EndLinePosition.Line - lineSpan.StartLinePosition.Line + 1;
            int startLine = lineSpan.StartLinePosition.Line + 1;
            int complexity = CalculateCyclomaticComplexity(method);

            methods.Add(new MethodComplexity(methodName, containingClass, normalizedPath, startLine, complexity, lineCount));
        }

        return Task.FromResult(new ComplexityAnalysisResult(methods, classes));
    }

    public static int CalculateCyclomaticComplexity(SyntaxNode node)
    {
        int complexity = 1;

        foreach (var child in node.DescendantNodes())
        {
            switch (child.Kind())
            {
                case SyntaxKind.IfStatement:
                case SyntaxKind.WhileStatement:
                case SyntaxKind.DoStatement:
                case SyntaxKind.ForStatement:
                case SyntaxKind.ForEachStatement:
                case SyntaxKind.CaseSwitchLabel:
                case SyntaxKind.CasePatternSwitchLabel:
                case SyntaxKind.SwitchExpressionArm:
                case SyntaxKind.CatchClause:
                case SyntaxKind.ConditionalExpression:
                case SyntaxKind.CoalesceExpression:
                    complexity++;
                    break;

                case SyntaxKind.LogicalAndExpression:
                case SyntaxKind.LogicalOrExpression:
                    complexity++;
                    break;
            }
        }

        return complexity;
    }
}
