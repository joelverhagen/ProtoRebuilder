namespace Knapcode.ProtoRebuilder;

public class ProtoFile
{
    public required string FileName { get; init; }
    public required string PackageName { get; init; }
    public required List<string> Imports { get; init; }
    public required string CsharpNamespace { get; init; }
    public required List<MessageInfo> MessageTypes { get; init; }
    public required List<EnumInfo> EnumTypes { get; init; }
}
