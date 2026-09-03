using Station.Infrastructure.Logging;

namespace Station.Core.Tests;

public sealed class SensitiveDataRedactorTests
{
    [Theory]
    [InlineData("Authorization")]
    [InlineData("roomToken")]
    [InlineData("mediaPath")]
    [InlineData("Cookie")]
    public void Sensitive_values_are_redacted(string key) => Assert.Equal("[REDACTED]", SensitiveDataRedactor.Redact(key, "private"));

    [Fact]
    public void Ordinary_values_are_preserved() => Assert.Equal("playing", SensitiveDataRedactor.Redact("state", "playing"));
}
