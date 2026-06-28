using ConfigAdmin.Application.RemoteSync;
using Xunit;

namespace ConfigAdmin.Tests;

public class PublicDnsResolverTests
{
    [Fact]
    public void ParseDohResponse_ExtractsIpv4Records()
    {
        var response = new PublicDnsResolver.DohJsonResponse
        {
            Status = 0,
            Answer =
            [
                new PublicDnsResolver.DohAnswer { Type = 5, Data = "ts.net" },
                new PublicDnsResolver.DohAnswer { Type = 1, Data = "100.70.234.31" }
            ]
        };

        var ips = PublicDnsResolver.ParseDohResponse(response);

        Assert.Single(ips);
        Assert.Equal("100.70.234.31", ips[0].ToString());
    }

    [Fact]
    public void ParseDohResponse_ReturnsEmpty_WhenStatusNotZero()
    {
        var response = new PublicDnsResolver.DohJsonResponse { Status = 3 };

        Assert.Empty(PublicDnsResolver.ParseDohResponse(response));
    }
}
