using Microsoft.Extensions.Logging;
namespace Knapcode.ProtoRebuilder;

public static class ProtoFileWriter
{
    public static void WriteProtoSchema(RebuilderContext ctx, ProtoFile protoFile, TextWriter writer)
    {
        writer.WriteLine("syntax = \"proto3\";");

        if (protoFile.PackageName.Length > 0)
        {
            writer.WriteLine($"package {protoFile.PackageName};");
        }

        if (protoFile.Imports.Count > 0)
        {
            foreach (var import in protoFile.Imports.Order(StringComparer.Ordinal))
            {
                writer.WriteLine($"import \"{import}\";");
            }
        }

        if (protoFile.CsharpNamespace.Length > 0)
        {
            writer.WriteLine($"option csharp_namespace = \"{protoFile.CsharpNamespace}\";");
        }

        foreach (var messageInfo in protoFile.MessageTypes)
        {
            GenerateMessageSchema(ctx, writer, messageInfo, depth: 0);
        }

        foreach (var enumInfo in protoFile.EnumTypes)
        {
            GenerateEnumSchema(ctx, writer, enumInfo, depth: 0);
        }
    }

    private static void GenerateMessageSchema(RebuilderContext ctx, TextWriter writer, MessageInfo messageInfo, int depth)
    {
        writer.WriteLine();
        writer.Write(new string(' ', depth * 2));
        writer.WriteLine($"message {messageInfo.Type.Name} {{");

        foreach (var field in messageInfo.Fields)
        {
            writer.Write(new string(' ', (depth + 1) * 2));
            var result = ProtoTypeMapper.GetProtoTypeAndImports(ctx, field.Property.PropertyType);
            var lowerSnakeFieldName = ProtoTypeMapper.PascalToLowerSnakeCase(field.Name);
            if (result.HasValue)
            {
                writer.WriteLine($"{result.Value.TypeName} {lowerSnakeFieldName} = {field.Number};");
            }
            else
            {
                ctx.Logger.LogWarning(
                    "  Using 'bytes' for unknown type {TypeFullName} (found in field {FieldNumber} of message {MessageName})",
                    field.Property.PropertyType.FullName,
                    field.Number,
                    messageInfo.Type.Name);
                writer.WriteLine($"bytes {lowerSnakeFieldName} = {field.Number}; // Unknown type: {field.Property.PropertyType.FullName}");
            }
        }

        foreach (var oneof in messageInfo.Oneofs)
        {
            writer.Write(new string(' ', (depth + 1) * 2));
            var lowerSnakeOneofName = ProtoTypeMapper.PascalToLowerSnakeCase(oneof.Name);
            writer.WriteLine($"oneof {lowerSnakeOneofName} {{");
            foreach (var field in oneof.Fields)
            {
                writer.Write(new string(' ', (depth + 2) * 2));
                var result = ProtoTypeMapper.GetProtoTypeAndImports(ctx, field.Property.PropertyType);
                var lowerSnakeFieldName = ProtoTypeMapper.PascalToLowerSnakeCase(field.Name);
                if (result.HasValue)
                {
                    writer.WriteLine($"{result.Value.TypeName} {lowerSnakeFieldName} = {field.Number};");
                }
                else
                {
                    ctx.Logger.LogWarning(
                        "  Using 'bytes' for unknown type {TypeFullName} (found in field {FieldNumber} of message {MessageName}, {OneofName} oneof)",
                        field.Property.PropertyType.FullName,
                        field.Number,
                        messageInfo.Type.Name,
                        lowerSnakeFieldName);
                    writer.WriteLine($"bytes {lowerSnakeFieldName} = {field.Number}; // Unknown type: {field.Property.PropertyType.FullName}");
                }
            }
            writer.Write(new string(' ', (depth + 1) * 2));
            writer.WriteLine("}");
        }

        foreach (var nestedEnum in messageInfo.NestedEnums)
        {
            GenerateEnumSchema(ctx, writer, nestedEnum, depth + 1);
        }

        foreach (var nestedMessage in messageInfo.NestedMessages)
        {
            GenerateMessageSchema(ctx, writer, nestedMessage, depth + 1);
        }

        writer.Write(new string(' ', depth * 2));
        writer.WriteLine("}");
    }

    private static void GenerateEnumSchema(RebuilderContext ctx, TextWriter writer, EnumInfo enumInfo, int depth)
    {
        writer.WriteLine();
        writer.Write(new string(' ', depth * 2));
        writer.WriteLine($"enum {enumInfo.Type.Name} {{");

        var typeNameAllCaps = ProtoTypeMapper.PascalToUpperSnakeCase(enumInfo.Type.Name);
        foreach (var field in enumInfo.Pairs.OrderByDescending(x => x.Value == 0).ThenBy(x => x.Value))
        {
            writer.Write(new string(' ', (depth + 1) * 2));
            writer.WriteLine($"{typeNameAllCaps}_{ProtoTypeMapper.PascalToUpperSnakeCase(field.Key)} = {field.Value};");
        }

        writer.Write(new string(' ', depth * 2));
        writer.WriteLine("}");
    }
}
