using Station.Application.Configuration;

namespace Station.Core.Tests;

public sealed class StationOptionsValidatorTests
{
    [Fact]
    public void Defaults_are_valid() => Assert.True(StationOptionsValidator.Validate(new StationOptions()).IsSuccess);

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    public void Invalid_port_is_rejected(int port)
    {
        var options = new StationOptions { Server = new ServerOptions { Port = port } };
        Assert.Equal("configuration.invalid", StationOptionsValidator.Validate(options).Error.Code);
    }
}
