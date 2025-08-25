using Mono.Cecil;
using System.Diagnostics;

namespace Knapcode.ProtoRebuilder;

[DebuggerDisplay("Message: {Type.FullName}")]
public class MessageInfo
{
    public bool IsRoot => RootMessage is null; 
    public required MessageInfo? RootMessage { get; init; }
    public required TypeDefinition Type { get; init; }
    public List<FieldInfo> Fields { get; } = [];
    public List<OneofInfo> Oneofs { get; } = [];
    public List<MessageInfo> NestedMessages { get; } = [];
    public List<EnumInfo> NestedEnums { get; } = [];

    public HashSet<string> Imports { get; } = [];
    public HashSet<MessageInfo> DependsOnMessages { get; } = [];
    public HashSet<EnumInfo> DependsOnEnums { get; } = [];
}
