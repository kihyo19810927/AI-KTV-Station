using Station.Application.Configuration;

namespace Station.Core.Tests;

public sealed class StationOptionsValidatorTests
{
    [Fact]
    public void Defaults_are_valid() => Assert.True(StationOptionsValidator.Validate(TestData.ValidOptions()).IsSuccess);

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    public void Invalid_port_is_rejected(int port)
    {
        var options = TestData.ValidOptions(port);
        Assert.Equal("configuration.invalid", StationOptionsValidator.Validate(options).Error.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("localhost")]
    [InlineData("0.0.0.0/path")]
    [InlineData("*")]
    public void Bind_address_must_be_an_explicit_ip_address(string bindAddress)
    {
        var baseline = TestData.ValidOptions();
        var options = new StationOptions
        {
            Server = new ServerOptions { BindAddress = bindAddress, Port = baseline.Server.Port },
            Storage = baseline.Storage,
            Player = baseline.Player,
        };
        Assert.Equal("configuration.invalid", StationOptionsValidator.Validate(options).Error.Code);
    }
}
