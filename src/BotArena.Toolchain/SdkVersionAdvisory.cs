namespace BotArena.Toolchain;

/// <summary>
/// Whether a project's declared <c>botarena.json</c> <c>sdkVersion</c> agrees
/// with the toolchain that is about to compile it.
///
/// <para>The field decides nothing: the build cache key carries the PINNED
/// <see cref="ToolchainInfo.SdkVersion"/> and the SHA-256 of the staged
/// Sdk/Guest DLLs (DECISIONS #84), so a stale declaration cannot produce a
/// stale artifact. That is exactly why it went unnoticed for a whole campaign —
/// projects declared 0.10.6 while building against 0.10.11 and everything was
/// green. It is still worth one line, because the declaration is what an author
/// reads when they ask "which SDK are these sources written for?", and a
/// scaffold copied from a frozen tree carries the frozen answer.</para>
///
/// <para>A WARNING, never a failure. Refusing would break every frozen
/// <c>arena-bots/</c> tree the moment the SDK moves, and reproducing a frozen
/// artifact from its frozen sources is a thing the campaign does on purpose.</para>
/// </summary>
public static class SdkVersionAdvisory
{
    /// <summary>
    /// The advisory line for <paramref name="manifest"/>, or
    /// <see langword="null"/> when the declaration already agrees with the
    /// toolchain.
    /// </summary>
    public static string? Describe(BotManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return Describe(manifest.SdkVersion);
    }

    /// <summary>The advisory line for one declared version string.</summary>
    public static string? Describe(string declaredSdkVersion)
    {
        if (string.Equals(
                declaredSdkVersion,
                ToolchainInfo.SdkVersion,
                StringComparison.Ordinal))
        {
            return null;
        }

        return $"botarena.json declares sdkVersion "
            + $"{declaredSdkVersion}, but this toolchain builds against "
            + $"{ToolchainInfo.SdkVersion}. The declaration changes nothing "
            + "about the artifact — the build always uses the toolchain's SDK "
            + "— so this is a stale label, not a broken build. Set "
            + $"\"sdkVersion\": \"{ToolchainInfo.SdkVersion}\" to keep it "
            + "honest.";
    }
}
