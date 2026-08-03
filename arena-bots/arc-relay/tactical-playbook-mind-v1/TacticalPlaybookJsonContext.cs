using System.Text.Json.Serialization;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(TacticalPlaybookPackage.Playbook))]
[JsonSerializable(typeof(TacticalPlaybookPackage.Layout))]
internal sealed partial class TacticalPlaybookJsonContext : JsonSerializerContext;
