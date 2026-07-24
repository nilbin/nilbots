using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace BotArena.App.Bots;

public sealed class SubmissionNetwork
{
    private readonly byte[] key;

    public SubmissionNetwork(string secret)
    {
        if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32)
            throw new InvalidOperationException(
                "BOTARENA_NETWORK_HASH_KEY must contain at least 32 characters.");
        key = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(secret));
    }

    /// <summary>
    /// Produces a server-keyed /24 IPv4 or /64 IPv6 pseudonym. Using a subnet
    /// makes account farming less effective; HMAC prevents the stored IPv4 key
    /// from being reversed with a small brute-force table.
    /// </summary>
    public string Hash(IPAddress? address)
    {
        address ??= IPAddress.None;
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        byte[] bytes = address.GetAddressBytes();
        int prefixBytes;
        byte family;
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            prefixBytes = 3;
            family = 4;
        }
        else
        {
            prefixBytes = Math.Min(8, bytes.Length);
            family = 6;
        }

        var input = new byte[prefixBytes + 1];
        input[0] = family;
        bytes.AsSpan(0, prefixBytes).CopyTo(input.AsSpan(1));
        return Convert.ToHexStringLower(HMACSHA256.HashData(key, input));
    }
}
