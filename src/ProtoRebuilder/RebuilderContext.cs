using Microsoft.Extensions.Logging;
using Mono.Cecil;

namespace Knapcode.ProtoRebuilder;

public class RebuilderContext
{
    public required ILogger Logger { get; init; }

    public required TypeDefinition MessageInterface { get; init; }
    public required TypeDefinition FileDescriptorType { get; init; }
    public required TypeDefinition RepeatedFieldType { get; init; }
    public required TypeDefinition MapFieldType { get; init; }

    public HashSet<MessageInfo> MessageTypes { get; } = [];
    public HashSet<EnumInfo> EnumTypes { get; } = [];

    public Dictionary<string, MessageInfo> FullNameToMessage { get; } = [];
    public Dictionary<string, EnumInfo> FullNameToEnum { get; } = [];

    public HashSet<ProtoModule> Modules { get; } = [];
    public Dictionary<MessageInfo, ProtoModule> MessageToModule { get; } = [];
    public Dictionary<EnumInfo, ProtoModule> EnumToModule { get; } = [];
}
