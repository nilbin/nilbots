namespace BotArena.Engine.Tests;

public sealed class ActorContractProfileAdmissionTests
{
    [Fact]
    public void CanonicalMatchAdmission_AcceptsExactNodeLimit()
    {
        string json = DenseNodeTree(
            BotArenaVersions.GenericActorMaxCanonicalContractNodes);

        ActorContractProfileAdmission.ValidateCanonicalMatch(json);
    }

    [Fact]
    public void CanonicalMatchAdmission_RejectsNodePastProfileLimit()
    {
        string json = DenseNodeTree(
            BotArenaVersions.GenericActorMaxCanonicalContractNodes + 1);

        ActorResolvedMatchValidationException error =
            Assert.Throws<ActorResolvedMatchValidationException>(() =>
                ActorContractProfileAdmission.ValidateCanonicalMatch(json));

        Assert.Contains("JSON values", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CanonicalMatchAdmission_AcceptsExactCollectionLimit()
    {
        string json = DenseArray(
            BotArenaVersions.GenericActorMaxCanonicalCollectionCount);

        ActorContractProfileAdmission.ValidateCanonicalMatch(json);
    }

    [Fact]
    public void CanonicalMatchAdmission_RejectsCollectionPastProfileLimit()
    {
        string json = DenseArray(
            BotArenaVersions.GenericActorMaxCanonicalCollectionCount + 1);

        ActorResolvedMatchValidationException error =
            Assert.Throws<ActorResolvedMatchValidationException>(() =>
                ActorContractProfileAdmission.ValidateCanonicalMatch(json));

        Assert.Contains(
            "direct values",
            error.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void CanonicalMatchAdmission_RejectsBytePastProfileLimit()
    {
        string json =
            "\"" +
            new string(
                'x',
                BotArenaVersions.GenericActorMaxCanonicalContractBytes) +
            "\"";

        ActorResolvedMatchValidationException error =
            Assert.Throws<ActorResolvedMatchValidationException>(() =>
                ActorContractProfileAdmission.ValidateCanonicalMatch(json));

        Assert.Contains("UTF-8 bytes", error.Message, StringComparison.Ordinal);
    }

    private static string DenseArray(int itemCount) =>
        "[" + string.Join(',', Enumerable.Repeat("0", itemCount)) + "]";

    private static string DenseNodeTree(int nodeCount)
    {
        if (nodeCount < 2)
            throw new ArgumentOutOfRangeException(nameof(nodeCount));

        int remaining = nodeCount - 1;
        var groups = new List<string>();
        while (remaining > 0)
        {
            int itemCount = Math.Min(
                BotArenaVersions.GenericActorMaxCanonicalCollectionCount,
                remaining - 1);
            groups.Add(DenseArray(itemCount));
            remaining -= itemCount + 1;
        }
        return "[" + string.Join(',', groups) + "]";
    }
}
