using System.Buffers.Binary;

namespace BotArena.App.Shared;

/// <summary>
/// PostgreSQL advisory lock keys for durable admission checks.
/// <para>
/// Every durable limit has the same race: two requests read a count below the limit at the
/// same time and both pass. The fix is always a transaction-scoped advisory lock on the
/// account, so the second waits and sees the first's row.
/// </para>
/// <para>
/// **Namespaced per subsystem, which is the point of putting them here.** Compilation and
/// ranked admission derived their key from the account id by identical arithmetic, so they
/// produced the *same* lock — a player's build and their ranked set queued behind each
/// other for no reason, and any future ordering difference between the two would have been
/// a deadlock rather than a slowdown. Mixing a subsystem tag in keeps them apart.
/// </para>
/// </summary>
public static class AdmissionLocks
{
    /// <summary>Subsystem tags. Distinct values; the specific numbers do not matter.</summary>
    private const long CompilationTag = 0x436F6D70_00000000; // "Comp"
    private const long RankedTag = 0x52616E6B_00000000;      // "Rank"
    private const long AuthTag = 0x41757468_00000000;        // "Auth"

    /// <summary>Serialises everything competing for the shared compiler queue.</summary>
    public const long CompilerQueue = 0x4e494c424f545301;

    public static long Compilation(Guid userId) => Account(CompilationTag, userId);

    public static long Ranked(Guid userId) => Account(RankedTag, userId);

    /// <summary>Keyed by identifier rather than account: a failed login has no account yet.</summary>
    public static long Auth(string identifier) => Key(AuthTag, identifier.GetHashCode());

    private static long Account(long tag, Guid userId) =>
        Key(tag, BinaryPrimitives.ReadInt32LittleEndian(userId.ToByteArray()));

    // The tag occupies the high half and the subject the low half, so two subsystems can
    // never collide however the subject hashes.
    private static long Key(long tag, int subject) => tag | (uint)subject;
}
