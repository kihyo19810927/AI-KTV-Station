using System.Net;
using Station.Server.Security;

namespace Station.Core.Tests;

public sealed class SecurityBoundaryTests
{
    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("::1", true)]
    [InlineData("192.168.1.20", false)]
    [InlineData("10.0.0.5", false)]
    public void Host_administration_accepts_only_loopback_remote_addresses(string address, bool expected) =>
        Assert.Equal(expected, LocalRequestPolicy.IsLocal(IPAddress.Parse(address)));

    [Fact]
    public async Task Responses_apply_browser_hardening_and_api_no_store_headers()
    {
        await using var factory = new StationApiTests.ApiFactory();
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/queue");

        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("DENY", Assert.Single(response.Headers.GetValues("X-Frame-Options")));
        Assert.Equal("no-referrer", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
    }
}
