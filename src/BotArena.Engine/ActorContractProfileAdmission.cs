using System.Text;
using System.Text.Json;

namespace BotArena.Engine;

/// <summary>
/// Final host admission limits for the canonical generic actor contract. The
/// matching SDK parser limits are part of the negotiated profile.
/// </summary>
internal static class ActorContractProfileAdmission
{
    public static void ValidateCanonicalMatch(string canonical)
    {
        ArgumentNullException.ThrowIfNull(canonical);

        int byteCount = Encoding.UTF8.GetByteCount(canonical);
        if (byteCount > BotArenaVersions.GenericActorMaxCanonicalContractBytes)
        {
            throw Invalid(
                $"Canonical contract is {byteCount} UTF-8 bytes; profile " +
                $"limit is " +
                $"{BotArenaVersions.GenericActorMaxCanonicalContractBytes}.");
        }

        byte[] utf8 = Encoding.UTF8.GetBytes(canonical);
        var reader = new Utf8JsonReader(
            utf8,
            new JsonReaderOptions
            {
                MaxDepth = 64,
                CommentHandling = JsonCommentHandling.Disallow,
            });
        var containers = new Stack<ContainerCount>();
        int nodeCount = 0;
        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.PropertyName:
                    IncrementContainer(
                        containers,
                        requireArray: false);
                    break;

                case JsonTokenType.StartObject:
                case JsonTokenType.StartArray:
                    IncrementArrayParent(containers);
                    CountNode(ref nodeCount);
                    containers.Push(
                        new ContainerCount(
                            reader.TokenType is JsonTokenType.StartArray,
                            0));
                    break;

                case JsonTokenType.EndObject:
                case JsonTokenType.EndArray:
                    if (containers.Count == 0)
                    {
                        throw Invalid(
                            "Canonical contract container nesting is malformed.");
                    }
                    containers.Pop();
                    break;

                case JsonTokenType.String:
                case JsonTokenType.Number:
                case JsonTokenType.True:
                case JsonTokenType.False:
                case JsonTokenType.Null:
                    IncrementArrayParent(containers);
                    CountNode(ref nodeCount);
                    break;
            }
        }
    }

    private static void CountNode(ref int nodeCount)
    {
        nodeCount = checked(nodeCount + 1);
        if (nodeCount
            > BotArenaVersions.GenericActorMaxCanonicalContractNodes)
        {
            throw Invalid(
                $"Canonical contract contains more than " +
                $"{BotArenaVersions.GenericActorMaxCanonicalContractNodes} " +
                "JSON values.");
        }
    }

    private static void IncrementArrayParent(
        Stack<ContainerCount> containers)
    {
        if (containers.TryPeek(out ContainerCount parent)
            && parent.IsArray)
        {
            IncrementContainer(containers, requireArray: true);
        }
    }

    private static void IncrementContainer(
        Stack<ContainerCount> containers,
        bool requireArray)
    {
        if (!containers.TryPop(out ContainerCount container)
            || container.IsArray != requireArray)
        {
            throw Invalid(
                "Canonical contract container token order is malformed.");
        }
        container = container with
        {
            Count = checked(container.Count + 1),
        };
        containers.Push(container);
        if (container.Count
            > BotArenaVersions.GenericActorMaxCanonicalCollectionCount)
        {
            throw Invalid(
                $"Canonical contract contains a JSON container with more " +
                $"than " +
                $"{BotArenaVersions.GenericActorMaxCanonicalCollectionCount} " +
                "direct values.");
        }
    }

    private static ActorResolvedMatchValidationException Invalid(
        string error) =>
        new([$"Generic actor profile admission: {error}"]);

    private readonly record struct ContainerCount(
        bool IsArray,
        int Count);
}
