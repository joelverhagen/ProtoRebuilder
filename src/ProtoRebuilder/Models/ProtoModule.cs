using System.Diagnostics;

namespace Knapcode.ProtoRebuilder;

[DebuggerDisplay("Module {Id} (removed = {IsRemoved}): {Namespace} ({RootMessages.Count} messages, {RootEnums.Count} enums, {DependsOn.Count} dependencies, {DependedOnBy.Count} dependents)")]
public class ProtoModule : IComparable<ProtoModule>
{
    private static int _nextId = 0;

    public int Id { get; } = Interlocked.Increment(ref _nextId);
    public bool IsRemoved { get; set; }

    public required string Namespace { get; init; }
    public HashSet<MessageInfo> RootMessages { get; } = [];
    public HashSet<EnumInfo> RootEnums { get; } = [];
    public HashSet<MessageInfo> DependsOnRootMessages { get; } = [];
    public HashSet<EnumInfo> DependsOnRootEnums { get; } = [];

    public HashSet<ProtoModule> DependsOn { get; } = [];
    public HashSet<ProtoModule> DependedOnBy { get; } = [];

    public void AddDependsOnEnum(EnumInfo related)
    {
        if (related.RootMessage is not null)
        {
            AddDependsOnMessage(related.RootMessage);
        }
        else
        {
            DependsOnRootEnums.Add(related);
        }
    }

    public void AddDependsOnMessage(MessageInfo messageInfo)
    {
        var root = messageInfo.RootMessage ?? messageInfo;
        if (RootMessages.Contains(root))
        {
            throw new InvalidOperationException("The message is already a root message in this module.");
        }

        DependsOnRootMessages.Add(root);
    }

    public void PopulateModuleLookup(RebuilderContext ctx)
    {
        foreach (var messageInfo in RootMessages)
        {
            ctx.MessageToModule[messageInfo] = this;
        }

        foreach (var enumInfo in RootEnums)
        {
            ctx.EnumToModule[enumInfo] = this;
        }
    }
    public int CompareTo(ProtoModule? other)
    {
        if (other is null)
        {
            return 1;
        }

        return Id.CompareTo(other.Id);
    }

    public void Merge(RebuilderContext ctx, ProtoModule other)
    {
        if (Namespace != other.Namespace)
        {
            throw new InvalidOperationException($"Cannot merge modules with different namespaces: '{Namespace}' and '{other.Namespace}'.");
        }

        if (ReferenceEquals(this, other))
        {
            throw new InvalidOperationException("Cannot merge a module with itself.");
        }

        RootMessages.UnionWith(other.RootMessages);
        RootEnums.UnionWith(other.RootEnums);
        DependsOnRootEnums.UnionWith(other.DependsOnRootEnums);
        DependsOnRootMessages.UnionWith(other.DependsOnRootMessages);

        DependsOnRootEnums.ExceptWith(RootEnums);
        DependsOnRootMessages.ExceptWith(RootMessages);

        foreach (var dependency in other.DependsOn)
        {
            dependency.DependedOnBy.Remove(other);
            dependency.DependedOnBy.Add(this);
        }

        foreach (var dependent in other.DependedOnBy)
        {
            dependent.DependsOn.Remove(other);
            dependent.DependsOn.Add(this);
        }

        DependsOn.UnionWith(other.DependsOn);
        DependedOnBy.UnionWith(other.DependedOnBy);

        DependedOnBy.Remove(this);
        DependsOn.Remove(this);

        PopulateModuleLookup(ctx);

        other.IsRemoved = true;
    }
}
