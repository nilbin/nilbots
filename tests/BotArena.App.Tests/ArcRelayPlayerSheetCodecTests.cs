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
}
