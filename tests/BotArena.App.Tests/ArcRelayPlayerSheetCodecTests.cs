using BotArena.App.ArcRelay;

namespace BotArena.App.Tests;

public sealed class ArcRelayPlayerSheetCodecTests
{
    private static readonly ArcRelayClassCatalog Catalog =
        ArcRelayClassCatalog.Default;
    private static readonly ArcRelayPlayerSheetCodec Codec = new(Catalog);

    [Fact]
    public void Template_is_valid_canonical_and_builds_without_rebuilding_the_mind()
    {
        ArcRelaySheetDocument document =
            ArcRelayPlayerSheetCodec.NewSheetTemplate();
        ArcRelaySheetCompilation first = Codec.Compile(
            document,
            Catalog.StarterIds,
            "sheet-a:r1");
        ArcRelaySheetCompilation second = Codec.Compile(
            Codec.Read(first.CanonicalJson),
            Catalog.StarterIds,
            "sheet-a:r1");

        Assert.Equal(8, first.Classes.Count);
        Assert.Equal(64, first.ContentHash.Length);
        Assert.Equal(first.ContentHash, second.ContentHash);
        Assert.Equal(first.CanonicalJson, second.CanonicalJson);
        Assert.Equal(first.LinkedData, second.LinkedData);
        Assert.True(first.LinkedData.Length < 64 * 1024);
    }

    [Fact]
    public void Locked_class_is_rejected_at_the_product_boundary()
    {
        ArcRelaySheetDocument original =
            ArcRelayPlayerSheetCodec.NewSheetTemplate();
        ArcRelaySheetDocument changed = original with
        {
            Slots = original.Slots.Select(slot => slot.UnitId == 0
                ? slot with { ClassId = "mortar" }
                : slot).ToArray(),
        };

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            Codec.Compile(changed, Catalog.StarterIds, "locked:r1"));

        Assert.Contains("locked", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void More_than_two_copies_of_one_class_is_rejected()
    {
        ArcRelaySheetDocument original =
            ArcRelayPlayerSheetCodec.NewSheetTemplate();
        ArcRelaySheetDocument changed = original with
        {
            Slots = original.Slots.Select(slot => slot.UnitId <= 2
                ? slot with { ClassId = "kestrel" }
                : slot).ToArray(),
        };

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            Codec.Compile(changed, Catalog.StarterIds, "copies:r1"));

        Assert.Contains("two-copy", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_exactly_marks_the_starter_unlocks()
    {
        Assert.Equal(16, Catalog.All.Count);
        Assert.Equal(
            Catalog.StarterIds.Order(StringComparer.Ordinal),
            Catalog.All.Where(value => value.Starter)
                .Select(value => value.Id)
                .Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Custom_mind_composition_uses_the_same_cap_and_reserves_adaptive_fields()
    {
        string[] valid = ArcRelayPlayerSheetCodec.NewSheetTemplate().Slots
            .OrderBy(value => value.UnitId).Select(value => value.ClassId).ToArray();
        ArcRelayCompositionCompilation compiled = ArcRelayComposition.Compile(
            new ArcRelayCompositionDeclaration(valid), Codec, Catalog.StarterIds);

        Assert.Equal(valid, ArcRelayComposition.Read(compiled.CanonicalJson).ClassIds);
        Assert.Equal(64, compiled.ContentHash.Length);
        Assert.Throws<InvalidDataException>(() => ArcRelayComposition.Compile(
            new ArcRelayCompositionDeclaration(valid, "adaptive-v1", []),
            Codec, Catalog.StarterIds));
        Assert.Throws<InvalidDataException>(() => ArcRelayComposition.Compile(
            new ArcRelayCompositionDeclaration(valid.Select((value, index) => index < 3 ? "kestrel" : value).ToArray()),
            Codec, Catalog.StarterIds));
    }

    [Fact]
    public void Crest_variants_are_deterministic_and_identity_scoped()
    {
        Guid first = Guid.Parse("642c9a64-272f-4b40-bce2-8d340c6340f8");
        ArcRelayCrestDescriptor a = ArcRelayCrestGenerator.Create(first, 17);
        ArcRelayCrestDescriptor repeated = ArcRelayCrestGenerator.Create(first, 17);
        ArcRelayCrestDescriptor otherVariant = ArcRelayCrestGenerator.Create(first, 18);

        Assert.Equal(a, repeated);
        Assert.NotEqual(a.Key, otherVariant.Key);
        Assert.NotEqual(a.Key, ArcRelayCrestGenerator.Create(Guid.NewGuid(), 17).Key);
    }

    [Fact]
    public void Felt_bar_trip_immediately_removes_the_entrant_from_pairing()
    {
        DateTime now = new(2026, 8, 2, 12, 0, 0, DateTimeKind.Utc);
        Guid matchId = Guid.NewGuid();
        var entrant = new ArcRelayEntrant
        {
            Name = "Convoy corrected",
            LadderOptedIn = true,
            LadderOptedInAt = now.AddHours(-1),
        };

        ArcRelayEntrantSuspension.Apply(
            entrant, matchId, ["sustained passivity", "handoff ping-pong", "sustained passivity"], now);

        Assert.False(entrant.LadderOptedIn);
        Assert.Null(entrant.LadderOptedInAt);
        Assert.Equal("handoff ping-pong, sustained passivity", entrant.SuspensionReason);
        Assert.Equal(matchId, entrant.SuspensionMatchId);
        Assert.Equal(now, entrant.SuspendedAt);
    }

    [Fact]
    public void Stale_preflight_cannot_admit_a_newer_mind_revision()
    {
        Guid currentMatch = Guid.NewGuid();
        var entrant = new ArcRelayEntrant
        {
            Name = "Revision pinned",
            PreflightStatus = ArcRelayPreflightStatus.Pending,
            PreflightMatchId = currentMatch,
            PreflightRevision = 2,
        };

        Assert.False(ArcRelayPreflightSettlement.ApplyIfCurrent(
            entrant, currentMatch, 1, 0, DateTime.UtcNow));
        Assert.False(ArcRelayPreflightSettlement.ApplyIfCurrent(
            entrant, Guid.NewGuid(), 2, 0, DateTime.UtcNow));
        Assert.Equal(ArcRelayPreflightStatus.Pending, entrant.PreflightStatus);

        Assert.True(ArcRelayPreflightSettlement.ApplyIfCurrent(
            entrant, currentMatch, 2, 0, DateTime.UtcNow));
        Assert.Equal(ArcRelayPreflightStatus.Passed, entrant.PreflightStatus);
    }
}
