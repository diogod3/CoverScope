using DD3.CoverScope;
using Xunit;

namespace DD3.CoverScope.Tests;

public partial class CoverScopeCommandLineTests
{
    [Fact]
    public void ShouldExposeInvalidInvocationDirectoryForCliTranslation()
    {
        Assert.Throws<ArgumentException>(
            () => CoverScopeCommandLine.Parse([], "invalid\0directory"));
    }
}
