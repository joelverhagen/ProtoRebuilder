using System.Text.RegularExpressions;
using Mono.Cecil;

namespace Knapcode.ProtoRebuilder;

public static class ProtoTypeMapper
{
    public static (string TypeName, IReadOnlyList<string> ExternalImports, IReadOnlyList<string> InternalTypes)? GetProtoTypeAndImports(RebuilderContext ctx, TypeReference type)
    {
        switch (type.FullName)
        {
            case "System.Double":
                return ("double", [], []);
            case "System.Single":
                return ("float", [], []);
            case "System.Int32":
                return ("int32", [], []);
            case "System.Int64":
                return ("int64", [], []);
            case "System.UInt32":
                return ("uint32", [], []);
            case "System.UInt64":
                return ("uint64", [], []);
            case "System.Boolean":
                return ("bool", [], []);
            case "System.String":
                return ("string", [], []);
            case "Google.Protobuf.ByteString":
                return ("bytes", [], []);
            case "Google.Protobuf.WellKnownTypes.Timestamp":
                return ("google.protobuf.Timestamp", ["google/protobuf/timestamp.proto"], []);
        }

        if (ctx.FullNameToMessage.ContainsKey(type.FullName)
            || ctx.FullNameToEnum.ContainsKey(type.FullName))
        {
            var rootType = type;
            if (type.DeclaringType is not null)
            {
                while (rootType.DeclaringType is not null)
                {
                    rootType = rootType.DeclaringType;
                }
            }

            var packagePart = string.IsNullOrEmpty(rootType.Namespace) ? "" : PascalToLowerSnakeCase(rootType.Namespace);

            var messageNamesPart = type
                .FullName
                .Substring(rootType.Namespace.Length)
                // handle nested types (nested messages/enums)
                .Replace("/", ".")
                // handle https://github.com/protocolbuffers/protobuf/blob/e81876d0581c3cae6ca6b036d9f345576c96695a/src/google/protobuf/compiler/csharp/names.cc#L61
                // this might need to be more sophisticated in the future
                .Replace(".Types.", ".");

            var protoName = packagePart + messageNamesPart;
            return (protoName, [], [type.FullName]); // imports for user-defined messages are handled separately
        }

        if (type.IsGenericInstance)
        {
            var genericInstance = (GenericInstanceType)type;

            const string nullable = "System.Nullable`1";
            if (genericInstance.ElementType.FullName == nullable)
            {
                if (genericInstance.GenericArguments.Count != 1)
                {
                    throw new InvalidOperationException($"{nullable} must have exactly one generic argument, but found {genericInstance.GenericArguments.Count}.");
                }

                var nullableType = GetProtoTypeAndImports(ctx, genericInstance.GenericArguments[0]);
                if (nullableType.HasValue)
                {
                    switch (nullableType.Value.TypeName)
                    {
                        case "bool":
                            return ("google.protobuf.BoolValue", ["google/protobuf/wrappers.proto"], []);
                        case "double":
                            return ("google.protobuf.DoubleValue", ["google/protobuf/wrappers.proto"], []);
                        case "float":
                            return ("google.protobuf.FloatValue", ["google/protobuf/wrappers.proto"], []);
                        case "int32":
                            return ("google.protobuf.Int32Value", ["google/protobuf/wrappers.proto"], []);
                        case "int64":
                            return ("google.protobuf.Int64Value", ["google/protobuf/wrappers.proto"], []);
                        case "uint32":
                            return ("google.protobuf.UInt32Value", ["google/protobuf/wrappers.proto"], []);
                        case "uint64":
                            return ("google.protobuf.UInt64Value", ["google/protobuf/wrappers.proto"], []);
                    }
                }
            }

            if (genericInstance.ElementType.FullName == ctx.RepeatedFieldType.FullName)
            {
                if (genericInstance.GenericArguments.Count != 1)
                {
                    throw new InvalidOperationException($"{ctx.RepeatedFieldType.FullName} must have exactly one generic argument, but found {genericInstance.GenericArguments.Count}.");
                }

                var repeatedType = GetProtoTypeAndImports(ctx, genericInstance.GenericArguments[0]);
                if (!repeatedType.HasValue)
                {
                    return null;
                }

                return (
                    $"repeated {repeatedType.Value.TypeName}",
                    repeatedType.Value.ExternalImports,
                    repeatedType.Value.InternalTypes);
            }

            if (genericInstance.ElementType.FullName == ctx.MapFieldType.FullName)
            {
                if (genericInstance.GenericArguments.Count != 2)
                {
                    throw new InvalidOperationException($"{ctx.MapFieldType.FullName} must have exactly two generic arguments, but found {genericInstance.GenericArguments.Count}.");
                }

                var keyType = GetProtoTypeAndImports(ctx, genericInstance.GenericArguments[0]);
                var valueType = GetProtoTypeAndImports(ctx, genericInstance.GenericArguments[1]);
                if (!keyType.HasValue || !valueType.HasValue)
                {
                    return null;
                }

                return (
                    $"map<{keyType.Value.TypeName}, {valueType.Value.TypeName}>",
                    keyType.Value.ExternalImports.Concat(valueType.Value.ExternalImports).ToList(),
                    keyType.Value.InternalTypes.Concat(valueType.Value.InternalTypes).ToList());
            }
        }

        return null;
    }

    public static string PascalToUpperSnakeCase(string input)
    {
        return Regex.Replace(input, "([a-z])([A-Z])", "$1_$2").ToUpperInvariant();
    }

    public static string PascalToLowerSnakeCase(string input)
    {
        return Regex.Replace(input, "([a-z])([A-Z])", "$1_$2").ToLowerInvariant();
    }
}
