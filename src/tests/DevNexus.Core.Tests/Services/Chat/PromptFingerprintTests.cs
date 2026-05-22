using DevNexus.Core.Services.Chat;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// Prompt 指纹测试。
/// </summary>
public sealed class PromptFingerprintTests
{
    /// <summary>
    /// 相同文本应生成相同 SHA-256 指纹。
    /// </summary>
    [Fact]
    public void ComputeHash_ShouldReturnStableLowercaseSha256()
    {
        var hash = PromptFingerprint.ComputeHash("DevNexus");

        hash.Should().Be("a36f998a108b23c1f020b10149e70c094f4a88304379c396067c3d0bfea7185c");
        hash.Should().HaveLength(64);
        hash.Should().Be(hash.ToLowerInvariant());
    }
}
