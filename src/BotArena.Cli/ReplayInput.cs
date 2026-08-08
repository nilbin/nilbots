using System.IO.Compression;
using System.Text;

namespace BotArena.Cli;

/// <summary>
/// Reads replay JSON from its canonical text form or a gzip transport. Gzip is
/// detected by its magic bytes rather than only by the filename, so an
/// evaluation artifact remains readable after a content-addressed rename.
/// </summary>
internal static class ReplayInput
{
    public static string ReadAllText(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using FileStream input = File.OpenRead(path);
        Stream payload = IsGzip(input)
            ? new GZipStream(input, CompressionMode.Decompress)
            : input;
        using (payload)
        using (var reader = new StreamReader(
                   payload,
                   new UTF8Encoding(
                       encoderShouldEmitUTF8Identifier: false,
                       throwOnInvalidBytes: true),
                   detectEncodingFromByteOrderMarks: true))
        {
            return reader.ReadToEnd();
        }
    }

    private static bool IsGzip(FileStream input)
    {
        int first = input.ReadByte();
        int second = input.ReadByte();
        input.Position = 0;
        return first == 0x1f && second == 0x8b;
    }
}
