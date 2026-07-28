using System.Security.Cryptography;
using System.Text;

namespace BotArena.Sdk.Tests;

public sealed class ActorSha256Tests
{
    [Theory]
    [InlineData(
        "",
        "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855")]
    [InlineData(
        "abc",
        "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad")]
    [InlineData(
        "The quick brown fox jumps over the lazy dog",
        "d7a8fbb307d7809469ca9abcb0082e4f8d5651e46d3cdb762d02d0bf37c9e592")]
    public void HashData_MatchesPublishedVectors(
        string value,
        string expected)
    {
        byte[] actual = ActorSha256.HashData(Encoding.UTF8.GetBytes(value));

        Assert.Equal(expected, Convert.ToHexStringLower(actual));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(55)]
    [InlineData(56)]
    [InlineData(63)]
    [InlineData(64)]
    [InlineData(65)]
    [InlineData(119)]
    [InlineData(120)]
    [InlineData(127)]
    [InlineData(128)]
    [InlineData(129)]
    [InlineData(1024)]
    [InlineData(1_048_575)]
    public void HashData_MatchesPlatformImplementationAcrossPaddingBoundaries(
        int length)
    {
        byte[] input = Enumerable.Range(0, length)
            .Select(index => (byte)(index * 131 + 17))
            .ToArray();

        Assert.Equal(
            SHA256.HashData(input),
            ActorSha256.HashData(input));
    }
}
