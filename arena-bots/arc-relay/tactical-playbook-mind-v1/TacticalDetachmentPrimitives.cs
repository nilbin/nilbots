internal static class TacticalDetachmentPrimitives
{
    internal sealed record Member(int UnitId, string RoleId, string ClassId);

    internal sealed record Selection(
        string OrderId,
        int Priority,
        string Kind,
        string[] Roles,
        string[] Classes,
        int Count);

    internal static IReadOnlyDictionary<int, string> Assign(
        IEnumerable<Member> members,
        IEnumerable<Selection> selections)
    {
        Member[] stableMembers = members
            .OrderBy(value => value.UnitId)
            .ToArray();
        Selection[] orderedSelections = selections
            .OrderBy(value => value.Priority)
            .ThenBy(value => value.OrderId, StringComparer.Ordinal)
            .ToArray();
        var result = new Dictionary<int, string>();

        foreach (Selection selection in orderedSelections)
        {
            Member[] eligible = stableMembers
                .Where(member => !result.ContainsKey(member.UnitId))
                .Where(member => selection.Roles.Length == 0
                    || selection.Roles.Contains(
                        member.RoleId,
                        StringComparer.Ordinal))
                .Where(member => selection.Classes.Length == 0
                    || selection.Classes.Contains(
                        member.ClassId,
                        StringComparer.Ordinal))
                .OrderBy(member => selection.Classes.Length == 0
                    ? 0
                    : Array.IndexOf(selection.Classes, member.ClassId))
                .ThenBy(member => member.UnitId)
                .ToArray();
            IEnumerable<Member> selected = selection.Kind switch
            {
                "all" => eligible,
                "take" => eligible.Take(selection.Count),
                "remainder" => eligible,
                _ => throw new InvalidDataException(
                    $"Unknown detachment selection '{selection.Kind}'."),
            };
            foreach (Member member in selected)
                result.Add(member.UnitId, selection.OrderId);
        }

        int[] unassigned = stableMembers
            .Where(member => !result.ContainsKey(member.UnitId))
            .Select(member => member.UnitId)
            .ToArray();
        if (unassigned.Length > 0)
        {
            throw new InvalidDataException(
                "Detachment selections left group members unassigned: "
                + string.Join(",", unassigned));
        }
        return result;
    }
}
