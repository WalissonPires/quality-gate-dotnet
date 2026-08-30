using FluentAssertions;
using QualityGate.Domain;
using Xunit;

namespace QualityGate.Tests.Domain;

public class ToolVersionTests
{
    [Fact]
    public void Current_ShouldReturnNonEmptyVersion()
    {
        var version = ToolVersion.Current;

        version.Should().NotBeNullOrWhiteSpace();
        version.Should().NotContain("+");
    }
}
