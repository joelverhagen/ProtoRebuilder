using Microsoft.Extensions.Logging;

namespace Knapcode.ProtoRebuilder;

public static class ModuleMerger
{
    public static void MergeModules(RebuilderContext ctx)
    {
        // find cycles in the same namespace and merge them into the same module
        ctx.Logger.LogInformation("");
        ctx.Logger.LogInformation("Merging module cycles...");
        var moduleCount = ctx.Modules.Count;
        MergeCyclesInSameNamespace(ctx);
        ctx.Logger.LogInformation("  Modules before: {ModuleCountBefore}, after: {ModuleCountAfter}", moduleCount, ctx.Modules.Count);

        // merge all modules that have the same namespace and have no dependencies
        ctx.Logger.LogInformation("Merging modules with no dependencies.");
        moduleCount = ctx.Modules.Count;
        MergeNodesAfterGrouping(ctx,
        [
            modules => modules.GroupBy(x => x.Namespace),
            modules => [modules.Where(x => x.DependsOn.Count == 0)],
        ]);
        ctx.Logger.LogInformation("  Modules before: {ModuleCountBefore}, after: {ModuleCountAfter}", moduleCount, ctx.Modules.Count);

        // merge all modules that have the same namespace and are not depended on by any other module
        ctx.Logger.LogInformation("Merging modules with no dependents.");
        moduleCount = ctx.Modules.Count;
        MergeNodesAfterGrouping(ctx,
        [
            modules => modules.GroupBy(x => x.Namespace),
            modules => [modules.Where(x => x.DependedOnBy.Count == 0)],
        ]);
        ctx.Logger.LogInformation("  Modules before: {ModuleCountBefore}, after: {ModuleCountAfter}", moduleCount, ctx.Modules.Count);

        // merge all modules that have the same namespace and have the same dependencies and dependents
        ctx.Logger.LogInformation("Merging modules with same dependencies and dependents.");
        moduleCount = ctx.Modules.Count;
        MergeNodesAfterGrouping(ctx,
        [
            modules => modules.GroupBy(x => x.Namespace),
            modules => modules.GroupBy(x => x.DependsOn, SetComparer<ProtoModule>.Instance),
            modules => modules.GroupBy(x => x.DependedOnBy, SetComparer<ProtoModule>.Instance),
        ]);
        ctx.Logger.LogInformation("  Modules before: {ModuleCountBefore}, after: {ModuleCountAfter}", moduleCount, ctx.Modules.Count);

        // brute force the rest
        ctx.Logger.LogInformation("Merging remaining modules until a cycle is created.");
        moduleCount = ctx.Modules.Count;
        MergeUntilCycle(ctx);
        ctx.Logger.LogInformation("  Modules before: {ModuleCountBefore}, after: {ModuleCountAfter}", moduleCount, ctx.Modules.Count);

        ctx.Logger.LogInformation("");
        ctx.Logger.LogInformation("Validating the final module graph...");
        ValidateGraph(ctx);
        ctx.Logger.LogInformation("No issues found.");
    }

    private static void MergeCyclesInSameNamespace(RebuilderContext ctx)
    {
        while (true)
        {
            var cycle = GetCycle(ctx);
            if (cycle is null)
            {
                break;
            }

            var first = cycle.First();
            var others = cycle.Skip(1).SkipLast(1);
            if (others.All(x => x.Namespace == first.Namespace))
            {
                foreach (var other in others)
                {
                    first.Merge(ctx, other);
                    ctx.Modules.Remove(other);
                }
            }
            else
            {
                break;
            }
        }
    }

    private static void MergeNodesAfterGrouping(
        RebuilderContext ctx,
        List<Func<IEnumerable<ProtoModule>, IEnumerable<IEnumerable<ProtoModule>>>> groupingStrategies)
    {
        void GroupAndMerge(IEnumerable<ProtoModule> modules, int strategyIndex)
        {
            if (!modules.Any())
            {
                return;
            }

            if (strategyIndex >= groupingStrategies.Count)
            {
                var combined = modules.First();
                foreach (var other in modules.Skip(1))
                {
                    combined.Merge(ctx, other);
                    ctx.Modules.Remove(other);
                }
            }
            else
            {
                var groups = groupingStrategies[strategyIndex](modules);
                foreach (var group in groups)
                {
                    GroupAndMerge(group, strategyIndex + 1);
                }
            }
        }

        GroupAndMerge(ctx.Modules, 0);
    }

    private static void MergeUntilCycle(RebuilderContext ctx)
    {
        bool changed = false;
        do
        {
            var removedModules = new HashSet<ProtoModule>();
            var mergedNodes = new HashSet<ProtoModule>();
            foreach (var group in ctx.Modules.GroupBy(x => x.Namespace).Select(x => x.ToList()))
            {
                for (var i = 0; i < group.Count; i++)
                {
                    for (var j = i + 1; j < group.Count; j++)
                    {
                        if (mergedNodes.Overlaps(removedModules))
                        {
                            continue;
                        }

                        mergedNodes.Clear();
                        mergedNodes.Add(group[i]);
                        mergedNodes.Add(group[j]);

                        if (HasCycle(ctx, mergedNodes))
                        {
                            continue;
                        }

                        group[i].Merge(ctx, group[j]);
                        removedModules.Add(group[j]);
                    }
                }
            }

            ctx.Modules.ExceptWith(removedModules);
            changed = removedModules.Count > 0;
        }
        while (changed);
    }

    private enum NodeState
    {
        Unvisited,
        Visiting,
        Visited,
    }

    private static List<ProtoModule>? GetCycle(RebuilderContext ctx)
    {
        var nodeToState = new Dictionary<ProtoModule, NodeState>();
        var path = new Stack<ProtoModule>();

        foreach (var node in ctx.Modules)
        {
            var cycle = GetCycle(node, nodeToState, path);
            if (cycle != null)
            {
                return cycle;
            }
        }

        return null;

        static List<ProtoModule>? GetCycle(ProtoModule node, Dictionary<ProtoModule, NodeState> nodeToState, Stack<ProtoModule> path)
        {
            if (!nodeToState.TryGetValue(node, out var state))
            {
                nodeToState[node] = NodeState.Visiting;
                path.Push(node);

                foreach (var neighbor in node.DependsOn)
                {
                    var cycle = GetCycle(neighbor, nodeToState, path);
                    if (cycle != null)
                    {
                        return cycle;
                    }
                }

                nodeToState[node] = NodeState.Visited;
                path.Pop();

                return null;
            }

            if (state == NodeState.Visiting)
            {
                var cycle = path.Reverse().SkipWhile(n => n != node).ToList();
                cycle.Add(node);
                return cycle;
            }

            return null;
        }
    }

    private static bool HasCycle(RebuilderContext ctx, HashSet<ProtoModule> mergedNodes)
    {
        var nodeToState = new Dictionary<ProtoModule, NodeState>();

        foreach (var node in ctx.Modules)
        {
            if (HasCycle(node, nodeToState, mergedNodes))
            {
                return true;
            }
        }

        return false;

        static bool HasCycle(ProtoModule node, Dictionary<ProtoModule, NodeState> nodeToState, HashSet<ProtoModule> mergedNodes)
        {
            if (mergedNodes.Contains(node))
            {
                if (!nodeToState.TryGetValue(node, out var state))
                {
                    if (mergedNodes.Any(nodeToState.ContainsKey))
                    {
                        throw new InvalidOperationException("There is inconsistent state between merged nodes.");
                    }

                    foreach (var other in mergedNodes)
                    {
                        nodeToState[other] = NodeState.Visiting;
                    }

                    foreach (var other in mergedNodes)
                    {
                        foreach (var neighbor in other.DependsOn)
                        {
                            if (mergedNodes.Contains(neighbor))
                            {
                                continue;
                            }

                            if (HasCycle(neighbor, nodeToState, mergedNodes))
                            {
                                return true;
                            }
                        }
                    }

                    foreach (var other in mergedNodes)
                    {
                        nodeToState[other] = NodeState.Visited;
                    }

                    return false;
                }

                if (state == NodeState.Visiting)
                {
                    foreach (var other in mergedNodes)
                    {
                        if (!nodeToState.TryGetValue(other, out var otherState)
                            || otherState != NodeState.Visiting)
                        {
                            throw new InvalidOperationException("There is inconsistent state between merged nodes.");
                        }
                    }

                    return true;
                }

                return false;
            }
            else
            {
                if (!nodeToState.TryGetValue(node, out var state))
                {
                    nodeToState[node] = NodeState.Visiting;

                    foreach (var neighbor in node.DependsOn)
                    {
                        if (HasCycle(neighbor, nodeToState, mergedNodes))
                        {
                            return true;
                        }
                    }

                    nodeToState[node] = NodeState.Visited;

                    return false;
                }

                if (state == NodeState.Visiting)
                {
                    return true;
                }

                return false;
            }
        }
    }

    private static void ValidateGraph(RebuilderContext ctx)
    {
        var cycle = GetCycle(ctx);
        if (cycle is not null)
        {
            throw new InvalidOperationException("There is a cycle in module dependencies.");
        }

        if (HasRemovedNode(ctx))
        {
            throw new InvalidOperationException("There are removed modules that are still referenced by other modules.");
        }
    }

    private static bool HasRemovedNode(RebuilderContext ctx)
    {
        foreach (var (_, module) in ctx.MessageToModule)
        {
            if (!ctx.Modules.Contains(module))
            {
                return true;
            }
        }

        foreach (var (_, module) in ctx.EnumToModule)
        {
            if (!ctx.Modules.Contains(module))
            {
                return true;
            }
        }

        foreach (var module in ctx.Modules)
        {
            if (!ctx.Modules.IsSupersetOf(module.DependsOn))
            {
                return true;
            }

            if (!ctx.Modules.IsSupersetOf(module.DependedOnBy))
            {
                return true;
            }
        }

        return false;
    }
}
