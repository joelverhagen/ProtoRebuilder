using Mono.Cecil;

namespace Knapcode.ProtoRebuilder;

public class FieldInfo
{
    public required string Name { get; init; }
    public required int Number { get; init; }
    public required bool HasOptionalProperty { get; init; }
    public required PropertyDefinition Property { get; init; }
    public required bool IsOneof { get; init; }
}
