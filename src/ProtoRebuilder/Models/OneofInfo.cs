namespace Knapcode.ProtoRebuilder;

public class OneofInfo
{
    public required string Name { get; init; }
    public required EnumInfo Enum { get; init; }
    public required IReadOnlyList<FieldInfo> Fields { get; init; }
}
