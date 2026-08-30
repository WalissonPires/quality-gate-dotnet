using FluentAssertions;
using QualityGate.Infrastructure.Roslyn;
using Xunit;

namespace QualityGate.Tests.Infrastructure;

public sealed class ComplexityAnalyzerTests
{
    private readonly ComplexityAnalyzer _analyzer = new();

    [Fact]
    public async Task AnalyzeSourceAsync_CalculatesMethodComplexityAndLines()
    {
        var code = """
        namespace Wamage;

        public class Calculator
        {
            public int Compute(int a, int b, bool flag)
            {
                if (a > 0 && b > 0)
                {
                    return a + b;
                }
                else if (flag)
                {
                    return a - b;
                }

                return 0;
            }
        }
        """;

        var result = await _analyzer.AnalyzeSourceAsync(code);

        result.Methods.Should().ContainSingle();
        var method = result.Methods[0];
        method.MethodName.Should().Be("Compute");
        method.ClassName.Should().Be("Calculator");
        // base = 1, if + 1, && + 1, else if + 1 = 4
        method.CyclomaticComplexity.Should().Be(4);
        method.LineCount.Should().BeGreaterThan(5);

        result.Classes.Should().ContainSingle();
        result.Classes[0].ClassName.Should().Be("Calculator");
    }

    [Fact]
    public async Task AnalyzeSourceAsync_WithSimpleMethod_HasComplexity1()
    {
        var code = """
        public class Service
        {
            public string Greet(string name) => $"Hello {name}";
        }
        """;

        var result = await _analyzer.AnalyzeSourceAsync(code);

        result.Methods.Should().ContainSingle();
        result.Methods[0].CyclomaticComplexity.Should().Be(1);
    }
}
