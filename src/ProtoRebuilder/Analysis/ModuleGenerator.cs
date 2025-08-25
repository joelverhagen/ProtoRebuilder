namespace Knapcode.ProtoRebuilder;

public static class ModuleGenerator
{
    public static void GatherEnumModules(RebuilderContext ctx)
    {
        var rootEnumByNamespace = ctx.EnumTypes.Where(x => x.IsRoot).GroupBy(x => x.Type.Namespace);
        foreach (var rootEnums in rootEnumByNamespace)
        {
            foreach (var enumInfo in rootEnums)
            {
                var module = new ProtoModule { Namespace = rootEnums.Key };
                module.RootEnums.Add(enumInfo);
                ctx.EnumToModule.Add(enumInfo, module);
                ctx.Modules.Add(module);
            }
        }
    }

    public static void GatherMessageModules(RebuilderContext ctx)
    {
        var rootMessages = ctx.MessageTypes.Where(x => x.IsRoot).ToList();
        var remainingRootMessages = new HashSet<MessageInfo>(rootMessages);
        foreach (var root in rootMessages)
        {
            if (!remainingRootMessages.Contains(root))
            {
                continue;
            }

            var module = new ProtoModule { Namespace = root.Type.Namespace };
            module.RootMessages.Add(root);

            foreach (var message in EnumerateNestedMessageModule(root))
            {
                foreach (var dependency in message.DependsOnMessages)
                {
                    if ((dependency.RootMessage ?? dependency) != root)
                    {
                        module.AddDependsOnMessage(dependency);
                    }
                }

                foreach (var dependency in message.DependsOnEnums)
                {
                    if (dependency.RootMessage != root)
                    {
                        module.AddDependsOnEnum(dependency);
                    }
                }
            }

            var existingModules = module
                .RootMessages
                .Select(x => ctx.MessageToModule.GetValueOrDefault(x)!)
                .Where(x => x is not null)
                .Distinct()
                .ToList();
            if (existingModules.Count > 0)
            {
                foreach (var other in existingModules)
                {
                    module.Merge(ctx, other);
                }
            }
            else
            {
                module.PopulateModuleLookup(ctx);
            }

            ctx.Modules.ExceptWith(existingModules);
            ctx.Modules.Add(module);
            remainingRootMessages.ExceptWith(module.RootMessages);
        }
    }

    private static IEnumerable<MessageInfo> EnumerateNestedMessageModule(MessageInfo message)
    {
        if (!message.IsRoot)
        {
            throw new ArgumentException("The provided message must be a root message.");
        }

        var remaining = new Stack<MessageInfo>([message]);
        while (remaining.Count > 0)
        {
            var current = remaining.Pop();
            yield return current;

            foreach (var related in current.NestedMessages)
            {
                remaining.Push(related);
            }
        }
    }

    public static void AddModuleDependencies(RebuilderContext ctx)
    {
        foreach (var module in ctx.Modules)
        {
            foreach (var root in module.DependsOnRootMessages)
            {
                var otherModule = ctx.MessageToModule[root];
                module.DependsOn.Add(otherModule);
                otherModule.DependedOnBy.Add(module);
            }

            foreach (var root in module.DependsOnRootEnums)
            {
                var otherModule = ctx.EnumToModule[root];
                module.DependsOn.Add(otherModule);
                otherModule.DependedOnBy.Add(module);
            }
        }
    }
}
