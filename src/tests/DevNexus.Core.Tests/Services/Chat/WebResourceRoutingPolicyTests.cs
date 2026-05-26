using DevNexus.Core.Services.Chat;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

public sealed class WebResourceRoutingPolicyTests
{
    [Theory]
    [InlineData("https://github.com/a574676848/auto-devnexus")]
    [InlineData("https://gitlab.com/group/project")]
    [InlineData("https://bitbucket.org/team/repo")]
    public void IsGitRepositoryUrl_ShouldReturnTrue_ForKnownRepositoryUrls(string url)
    {
        WebResourceRoutingPolicy.IsGitRepositoryUrl(url).Should().BeTrue();
    }

    [Theory]
    [InlineData("https://github.com/a574676848")]
    [InlineData("https://example.com/blog/post")]
    [InlineData("not-a-url")]
    public void IsGitRepositoryUrl_ShouldReturnFalse_ForNonRepositoryUrls(string url)
    {
        WebResourceRoutingPolicy.IsGitRepositoryUrl(url).Should().BeFalse();
    }
}
