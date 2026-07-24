using System.Net;
using BotArena.App.Bots;

namespace BotArena.App.Tests;

public class SubmissionNetworkTests
{
    private readonly SubmissionNetwork network =
        new("test-only-network-hmac-key-32-characters");

    [Fact]
    public void Hash_GroupsIpv4SubnetWithoutStoringRawAddress()
    {
        string first = network.Hash(IPAddress.Parse("203.0.113.4"));
        string second = network.Hash(IPAddress.Parse("203.0.113.250"));
        string other = network.Hash(IPAddress.Parse("203.0.114.4"));

        Assert.Equal(first, second);
        Assert.NotEqual(first, other);
        Assert.Equal(64, first.Length);
        Assert.DoesNotContain("203", first);
    }

    [Fact]
    public void Hash_GroupsIpv6PrefixAndMappedIpv4()
    {
        Assert.Equal(
            network.Hash(IPAddress.Parse("2001:db8:1234:5678::1")),
            network.Hash(IPAddress.Parse("2001:db8:1234:5678::ffff")));
        Assert.Equal(
            network.Hash(IPAddress.Parse("192.0.2.10")),
            network.Hash(IPAddress.Parse("::ffff:192.0.2.10")));
    }
}
