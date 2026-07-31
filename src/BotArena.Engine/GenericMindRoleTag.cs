using System.Text;

namespace BotArena.Engine;

/// <summary>
/// The role-tag value rule, engine side
/// (<c>docs/DESIGN-MIND-ARCHITECTURE-2026-07-31.md</c> §12.1). A tag is a
/// canonical lowercase-kebab semantic ID capped at 24 UTF-8 bytes rather than
/// the 64-byte semantic-ID cap, because it is a display label sent per body per
/// tick and the budget should be visibly tight: 24 x 9 = 216 bytes worst case.
/// <para>
/// The tag carries NO authority. It cannot affect simulation state, it is never
/// an action parameter, and the engine never branches on it. The engine's only
/// jobs are to bound it, to remember the last one each body was given, and to
/// publish it — on the owning mind's bodies and on visible enemies alike, which
/// is what makes a deliberately wrong label a real move rather than a bug.
/// </para>
/// <para>
/// The empty string is legal and means CLEAR; an absent field on a command
/// means "leave the current tag alone". Those are two different things and the
/// chronology re-derivation depends on the difference.
/// </para>
/// </summary>
public static class GenericMindRoleTag
{
    private static readonly UTF8Encoding StrictUtf8 =
        new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>The visibly tight cap (§12.1), not the 64-byte semantic cap.</summary>
    public const int MaxUtf8Bytes = 24;

    /// <summary>
    /// True for the clear sentinel (the empty string) and for a canonical
    /// lowercase-kebab semantic ID within the cap. Everything else fails
    /// closed.
    /// </summary>
    public static bool IsValid(string? value)
    {
        if (value is null)
            return false;
        if (value.Length == 0)
            return true;
        if (Utf8ByteCount(value) > MaxUtf8Bytes)
            return false;

        bool needsSegmentStart = true;
        foreach (char character in value)
        {
            if (character == '-')
            {
                if (needsSegmentStart)
                    return false;
                needsSegmentStart = true;
                continue;
            }
            if (character is not (>= 'a' and <= 'z')
                and not (>= '0' and <= '9'))
            {
                return false;
            }
            needsSegmentStart = false;
        }
        return !needsSegmentStart;
    }

    /// <summary>
    /// Applies one command's tag field to a remembered tag. Null leaves it
    /// unchanged, the empty string clears it, and anything else replaces it.
    /// This is the exact function the chronology re-derives, so a document
    /// publishing a tag the mind never set is refused.
    /// </summary>
    public static string? Apply(string? remembered, string? commanded) =>
        commanded switch
        {
            null => remembered,
            "" => null,
            _ => commanded,
        };

    private static int Utf8ByteCount(string value)
    {
        try
        {
            return StrictUtf8.GetByteCount(value);
        }
        catch (EncoderFallbackException)
        {
            return int.MaxValue;
        }
    }
}
