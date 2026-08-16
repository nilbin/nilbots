using System.Text;
using BotArena.App.Sheets;

namespace BotArena.App.Tests;

public sealed class TacticalSheetWorkshopValidatorTests
{
    [Fact]
    public void Normalize_captures_one_stable_json_value_for_practice()
    {
        const string source = "{ \"schema\": \"arc-relay-workshop-v1\", \"plays\": [ ] }";

        Assert.Equal(
            "{\"schema\":\"arc-relay-workshop-v1\",\"plays\":[]}",
            TacticalSheetWorkshopValidator.Normalize(source));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("{\"schema\":\"arc-relay-workshop-v1\",\"plays\":[]}")]
    public void Accepts_absent_or_current_editor_metadata(string? value)
    {
        TacticalSheetWorkshopValidator.Validate(value);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("{\"schema\":\"wrong\",\"plays\":[]}")]
    [InlineData("{\"schema\":\"arc-relay-workshop-v1\"}")]
    [InlineData("{\"schema\":\"arc-relay-workshop-v1\",\"plays\":[],\"runtime\":{}}")]
    public void Rejects_non_workshop_shapes(string value)
    {
        Assert.Throws<InvalidDataException>(() =>
            TacticalSheetWorkshopValidator.Validate(value));
    }

    [Fact]
    public void Rejects_metadata_over_the_storage_bound()
    {
        string value = "{\"schema\":\"arc-relay-workshop-v1\",\"plays\":[],\"padding\":\""
            + new string('x', TacticalSheetWorkshopValidator.MaximumBytes)
            + "\"}";

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            TacticalSheetWorkshopValidator.Validate(value));
        Assert.Contains("exceeds", error.Message, StringComparison.Ordinal);
        Assert.True(Encoding.UTF8.GetByteCount(value)
            > TacticalSheetWorkshopValidator.MaximumBytes);
    }
}
