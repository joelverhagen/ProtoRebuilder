namespace Knapcode.ProtoRebuilder;

public static class LogicalAnalyzer
{
    public static void ConnectChildTypes(RebuilderContext ctx)
    {
        foreach (var message in ctx.MessageTypes)
        {
            ConnectChildTypes(ctx, message);
        }

        var rootMessages = new Stack<MessageInfo>(ctx.MessageTypes.Where(x => x.IsRoot));
        var allDiscovered = new List<MessageInfo>();
        while (rootMessages.Count > 0)
        {
            var current = rootMessages.Pop();
            allDiscovered.Add(current);
            foreach (var nested in current.NestedMessages)
            {
                rootMessages.Push(nested);
            }
        }

        var unlinkedMessages = ctx.MessageTypes.Except(allDiscovered).ToList();
        if (unlinkedMessages.Count > 0)
        {
            throw new InvalidOperationException($"{unlinkedMessages.Count} messages are nested via the root messages and are disconnected.");
        }
    }

    private static void ConnectChildTypes(RebuilderContext ctx, MessageInfo message)
    {
        var types = message.Type.NestedTypes.FirstOrDefault(x => x.IsAbstract && x.IsSealed && x.Name == "Types");

        foreach (var nestedType in message.Type.NestedTypes.Concat(types?.NestedTypes ?? []))
        {
            if (ctx.FullNameToMessage.TryGetValue(nestedType.FullName, out var nestedMessage))
            {
                message.NestedMessages.Add(nestedMessage);
            }

            if (ctx.FullNameToEnum.TryGetValue(nestedType.FullName, out var nestedEnum))
            {
                message.NestedEnums.Add(nestedEnum);
            }
        }
    }

    public static void RemoveUnreferencedEnums(RebuilderContext ctx)
    {
        var referencedEnumFullNames = new HashSet<string>();
        var referencedEnums = new List<EnumInfo>();
        foreach (var message in ctx.MessageTypes)
        {
            var fields = message.Fields.Concat(message.Oneofs.SelectMany(o => o.Fields));
            foreach (var field in fields)
            {

                var result = ProtoTypeMapper.GetProtoTypeAndImports(ctx, field.Property.PropertyType);
                if (result.HasValue)
                {
                    foreach (var type in result.Value.InternalTypes)
                    {
                        if (ctx.FullNameToEnum.TryGetValue(type, out var enumInfo))
                        {
                            if (referencedEnumFullNames.Add(type))
                            {
                                referencedEnums.Add(enumInfo);
                            }
                        }
                    }
                }
            }
        }

        ctx.EnumTypes.Clear();
        ctx.FullNameToEnum.Clear();
        ctx.EnumTypes.UnionWith(referencedEnums);
        foreach (var enumInfo in referencedEnums)
        {
            ctx.FullNameToEnum.Add(enumInfo.Type.FullName, enumInfo);
        }
    }

    public static void PopulateMessageDependencies(RebuilderContext ctx)
    {
        foreach (var message in ctx.MessageTypes)
        {
            PopulateMessageDependencies(ctx, message);
        }
    }

    private static void PopulateMessageDependencies(RebuilderContext ctx, MessageInfo message)
    {
        foreach (var field in message.Fields)
        {
            PopulateMessageDependencies(ctx, message, field);
        }

        foreach (var oneof in message.Oneofs)
        {
            foreach (var field in oneof.Fields)
            {
                PopulateMessageDependencies(ctx, message, field);
            }
        }

        foreach (var nestedMessage in message.NestedMessages)
        {
            PopulateMessageDependencies(ctx, nestedMessage);
        }
    }

    private static void PopulateMessageDependencies(RebuilderContext ctx, MessageInfo message, FieldInfo field)
    {
        var type = ProtoTypeMapper.GetProtoTypeAndImports(ctx, field.Property.PropertyType);
        if (type.HasValue)
        {
            message.Imports.UnionWith(type.Value.ExternalImports);

            foreach (var fullName in type.Value.InternalTypes)
            {
                if (ctx.FullNameToEnum.TryGetValue(fullName, out var dependsOnEnum))
                {
                    message.DependsOnEnums.Add(dependsOnEnum);
                }
                else
                {
                    var dependsOnMessage = ctx.FullNameToMessage[fullName];
                    message.DependsOnMessages.Add(dependsOnMessage);
                }
            }
        }
    }
}
