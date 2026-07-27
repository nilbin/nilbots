using System.Buffers.Binary;

namespace BotArena.Sdk;

/// <summary>
/// Dependency-free SHA-256 for canonical contract verification in NativeAOT
/// WASI guests, where System.Security.Cryptography is unavailable.
/// </summary>
internal static class ActorSha256
{
    private static ReadOnlySpan<uint> RoundConstants =>
    [
        0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5,
        0x3956c25b, 0x59f111f1, 0x923f82a4, 0xab1c5ed5,
        0xd807aa98, 0x12835b01, 0x243185be, 0x550c7dc3,
        0x72be5d74, 0x80deb1fe, 0x9bdc06a7, 0xc19bf174,
        0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc,
        0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da,
        0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7,
        0xc6e00bf3, 0xd5a79147, 0x06ca6351, 0x14292967,
        0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13,
        0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85,
        0xa2bfe8a1, 0xa81a664b, 0xc24b8b70, 0xc76c51a3,
        0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070,
        0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5,
        0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3,
        0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208,
        0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2,
    ];

    public static byte[] HashData(ReadOnlySpan<byte> data)
    {
        uint h0 = 0x6a09e667;
        uint h1 = 0xbb67ae85;
        uint h2 = 0x3c6ef372;
        uint h3 = 0xa54ff53a;
        uint h4 = 0x510e527f;
        uint h5 = 0x9b05688c;
        uint h6 = 0x1f83d9ab;
        uint h7 = 0x5be0cd19;

        int completeLength = data.Length & ~63;
        for (int offset = 0; offset < completeLength; offset += 64)
        {
            Compress(
                data.Slice(offset, 64),
                ref h0,
                ref h1,
                ref h2,
                ref h3,
                ref h4,
                ref h5,
                ref h6,
                ref h7);
        }

        int remainderLength = data.Length - completeLength;
        int finalLength = remainderLength < 56 ? 64 : 128;
        Span<byte> finalBlocks = stackalloc byte[128];
        finalBlocks.Clear();
        data[completeLength..].CopyTo(finalBlocks);
        finalBlocks[remainderLength] = 0x80;
        BinaryPrimitives.WriteUInt64BigEndian(
            finalBlocks.Slice(finalLength - 8, 8),
            checked((ulong)data.Length * 8));
        for (int offset = 0; offset < finalLength; offset += 64)
        {
            Compress(
                finalBlocks.Slice(offset, 64),
                ref h0,
                ref h1,
                ref h2,
                ref h3,
                ref h4,
                ref h5,
                ref h6,
                ref h7);
        }

        byte[] result = new byte[32];
        Span<byte> output = result;
        BinaryPrimitives.WriteUInt32BigEndian(output[0..4], h0);
        BinaryPrimitives.WriteUInt32BigEndian(output[4..8], h1);
        BinaryPrimitives.WriteUInt32BigEndian(output[8..12], h2);
        BinaryPrimitives.WriteUInt32BigEndian(output[12..16], h3);
        BinaryPrimitives.WriteUInt32BigEndian(output[16..20], h4);
        BinaryPrimitives.WriteUInt32BigEndian(output[20..24], h5);
        BinaryPrimitives.WriteUInt32BigEndian(output[24..28], h6);
        BinaryPrimitives.WriteUInt32BigEndian(output[28..32], h7);
        return result;
    }

    private static void Compress(
        ReadOnlySpan<byte> block,
        ref uint h0,
        ref uint h1,
        ref uint h2,
        ref uint h3,
        ref uint h4,
        ref uint h5,
        ref uint h6,
        ref uint h7)
    {
        Span<uint> schedule = stackalloc uint[64];
        for (int index = 0; index < 16; index++)
        {
            schedule[index] = BinaryPrimitives.ReadUInt32BigEndian(
                block.Slice(index * 4, 4));
        }
        for (int index = 16; index < schedule.Length; index++)
        {
            uint before15 = schedule[index - 15];
            uint before2 = schedule[index - 2];
            uint sigma0 =
                RotateRight(before15, 7)
                ^ RotateRight(before15, 18)
                ^ (before15 >> 3);
            uint sigma1 =
                RotateRight(before2, 17)
                ^ RotateRight(before2, 19)
                ^ (before2 >> 10);
            schedule[index] = unchecked(
                schedule[index - 16]
                + sigma0
                + schedule[index - 7]
                + sigma1);
        }

        uint a = h0;
        uint b = h1;
        uint c = h2;
        uint d = h3;
        uint e = h4;
        uint f = h5;
        uint g = h6;
        uint h = h7;
        ReadOnlySpan<uint> constants = RoundConstants;
        for (int index = 0; index < schedule.Length; index++)
        {
            uint sum1 =
                RotateRight(e, 6)
                ^ RotateRight(e, 11)
                ^ RotateRight(e, 25);
            uint choice = (e & f) ^ (~e & g);
            uint temporary1 = unchecked(
                h
                + sum1
                + choice
                + constants[index]
                + schedule[index]);
            uint sum0 =
                RotateRight(a, 2)
                ^ RotateRight(a, 13)
                ^ RotateRight(a, 22);
            uint majority = (a & b) ^ (a & c) ^ (b & c);
            uint temporary2 = unchecked(sum0 + majority);

            h = g;
            g = f;
            f = e;
            e = unchecked(d + temporary1);
            d = c;
            c = b;
            b = a;
            a = unchecked(temporary1 + temporary2);
        }

        h0 = unchecked(h0 + a);
        h1 = unchecked(h1 + b);
        h2 = unchecked(h2 + c);
        h3 = unchecked(h3 + d);
        h4 = unchecked(h4 + e);
        h5 = unchecked(h5 + f);
        h6 = unchecked(h6 + g);
        h7 = unchecked(h7 + h);
    }

    private static uint RotateRight(uint value, int count) =>
        (value >> count) | (value << (32 - count));
}
