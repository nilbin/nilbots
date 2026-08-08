using System.Text;

namespace BotArena.Sdk;

/// <summary>
/// Value rules unique to the mind profile. Everything else reuses
/// <see cref="GenericActorDynamicValueRules"/> unchanged, which is what keeps
/// a mind body and a per-life self encoding the same facts the same way.
/// </summary>
internal static class MindValueRules
{
    private static readonly UTF8Encoding StrictUtf8 =
        new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// A role tag is a canonical lowercase-kebab semantic ID capped at 24 UTF-8
    /// bytes rather than the 64-byte semantic-ID cap, because it is a display
    /// label sent per body per tick and the budget should be visibly tight. The
    /// EMPTY string is legal and means "clear the tag"; an absent field means
    /// "leave it unchanged".
    /// </summary>
    public static string RoleTag(string value, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);
        if (value.Length == 0)
            return value;

        if (StrictUtf8.GetByteCount(value)
            > GenericMindContractVersions.MaxRoleTagUtf8Bytes)
        {
            throw new ArgumentException(
                $"A role tag may not exceed "
                + $"{GenericMindContractVersions.MaxRoleTagUtf8Bytes} UTF-8 bytes.",
                parameterName);
        }
        return GenericActorDynamicValueRules.SemanticId(value, parameterName);
    }

    /// <summary>
    /// An inter-mind intent tag (RESERVED). Nothing in v1 may declare one, but
    /// the rule is written beside the field IDs it belongs to so the shape is
    /// fixed rather than re-litigated.
    /// </summary>
    public static string IntentTagId(string value, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);
        if (StrictUtf8.GetByteCount(value)
            > GenericMindContractVersions.MaxIntentTagUtf8Bytes)
        {
            throw new ArgumentException(
                $"An intent tag may not exceed "
                + $"{GenericMindContractVersions.MaxIntentTagUtf8Bytes} UTF-8 bytes.",
                parameterName);
        }
        return GenericActorDynamicValueRules.SemanticId(value, parameterName);
    }
}
