namespace Cartex.Domain.Authorization;

public static class PermissionDependencies
{
    public static IReadOnlyDictionary<string, string[]> Map { get; } =
        AppPermissions.Definitions.ToDictionary(
            x => x.Key,
            x => x.Value.DependsOn.ToArray());

    public static string[] DependsOn(string permission) =>
        Map.TryGetValue(permission, out var dependencies) ? dependencies : [];

    public static IReadOnlySet<string> RequiredFor(IEnumerable<string> permissions)
    {
        var result = new HashSet<string>();
        var stack = new Stack<string>(permissions);
        while (stack.Count > 0)
            foreach (var dependency in DependsOn(stack.Pop()))
                if (result.Add(dependency))
                    stack.Push(dependency);
        return result;
    }

    public static IReadOnlySet<string> Effective(IEnumerable<string> permissions)
    {
        var requested = permissions.ToHashSet();
        requested.UnionWith(RequiredFor(requested));
        return requested;
    }

    public static IReadOnlySet<string> DependentsOf(string permission) =>
        Map.Keys
            .Where(candidate => RequiredFor([candidate]).Contains(permission))
            .ToHashSet();
}
