using Mono.Cecil;
using System.Diagnostics;

namespace Knapcode.ProtoRebuilder;

[DebuggerDisplay("Enum: {Type.FullName}")]
public class EnumInfo
{
    public bool IsRoot => RootMessage is null;
    public required MessageInfo? RootMessage { get; init; }
    public required TypeDefinition Type { get; init; }
    public required IReadOnlyList<KeyValuePair<string, int>> Pairs { get; init; }
}
