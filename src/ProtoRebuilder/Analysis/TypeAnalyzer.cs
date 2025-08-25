using Mono.Cecil;

namespace Knapcode.ProtoRebuilder;

public static class TypeAnalyzer
{
    public static void GatherRelevantTypes(RebuilderContext ctx, AssemblyDefinition targetAssembly)
    {
        foreach (var type in targetAssembly.MainModule.Types.OrderBy(x => x.FullName, StringComparer.Ordinal))
        {
            GatherRelevantTypes(ctx, type, rootMessage: null);
        }
    }

    private static void GatherRelevantTypes(RebuilderContext ctx, TypeDefinition type, MessageInfo? rootMessage)
    {
        MessageInfo? messageInfo = null;
        if (type.Interfaces.Any(i => i.InterfaceType.Resolve() == ctx.MessageInterface))
        {
            messageInfo = new MessageInfo { Type = type, RootMessage = rootMessage };
            ctx.MessageTypes.Add(messageInfo);
            ctx.FullNameToMessage.Add(type.FullName, messageInfo);
        }

        if (type.IsEnum)
        {
            var pairs = type.Fields
                .Where(f => f.IsLiteral && f.HasConstant)
                .Select(f => new KeyValuePair<string, int>(f.Name, (int)f.Constant))
                .OrderBy(kvp => kvp.Value)
                .ToList();
            var enumInfo = new EnumInfo { Type = type, RootMessage = rootMessage, Pairs = pairs };
            ctx.EnumTypes.Add(enumInfo);
            ctx.FullNameToEnum.Add(type.FullName, enumInfo);
        }

        foreach (var nested in type.NestedTypes.OrderBy(x => x.FullName, StringComparer.Ordinal))
        {
            GatherRelevantTypes(ctx, nested, rootMessage ?? messageInfo);
        }
    }

    public static void PopulateMessageFields(RebuilderContext ctx)
    {
        foreach (var message in ctx.MessageTypes)
        {
            PopulateMessageFields(ctx, message);
        }
    }

    private static void PopulateMessageFields(RebuilderContext ctx, MessageInfo message)
    {
        var fieldNumberToInfo = new Dictionary<int, FieldInfo>();

        foreach (var property in message.Type.Properties)
        {
            const string caseSuffix = "Case";
            if (property.GetMethod?.HasThis == true
                && property.PropertyType.Resolve().IsEnum
                && property.PropertyType.Name.EndsWith("OneofCase", StringComparison.Ordinal)
                && property.Name.EndsWith(caseSuffix, StringComparison.Ordinal))
            {
                var oneofName = property.Name.Substring(0, property.Name.Length - caseSuffix.Length);
                var oneofEnumType = ctx.EnumTypes.First(x => x.Type == property.PropertyType.Resolve());
                var oneofFields = new List<FieldInfo>();
                foreach (var field in oneofEnumType.Type.Fields)
                {
                    if (field.IsStatic && field.IsLiteral)
                    {
                        var number = (int)field.Constant;
                        if (number > 0)
                        {
                            var matchingProperty = GetProperty(ctx, message.Type, field.Name);
                            if (matchingProperty is null)
                            {
                                throw new InvalidOperationException($"No property was found for oneof {oneofName} field {field.Name} on message {message.Type.FullName}.");
                            }

                            var fieldInfo = new FieldInfo
                            {
                                Name = field.Name,
                                Number = number,
                                HasOptionalProperty = HasOptionalProperty(message.Type, field.Name),
                                Property = matchingProperty,
                                IsOneof = true,
                            };

                            fieldNumberToInfo.Add(fieldInfo.Number, fieldInfo);
                            oneofFields.Add(fieldInfo);
                        }
                    }
                }

                message.Oneofs.Add(new OneofInfo
                {
                    Name = oneofName,
                    Enum = oneofEnumType,
                    Fields = oneofFields,
                });
            }
        }

        const string fieldNumberSuffix = "FieldNumber";
        foreach (var field in message.Type.Fields)
        {
            if (field.IsLiteral
                && field.HasConstant
                && field.Name.EndsWith(fieldNumberSuffix)
                && field.Constant is int)
            {
                var fieldName = field.Name.Substring(0, field.Name.Length - fieldNumberSuffix.Length);

                var matchingProperty = GetProperty(ctx, message.Type, fieldName);
                if (matchingProperty is null)
                {
                    throw new InvalidOperationException($"No property was found for field {field.Name} on message {message.Type.FullName}.");
                }

                var fieldInfo = new FieldInfo
                {
                    Name = fieldName,
                    Number = (int)field.Constant,
                    HasOptionalProperty = HasOptionalProperty(message.Type, field.Name),
                    Property = matchingProperty,
                    IsOneof = false,
                };

                if (!fieldNumberToInfo.TryGetValue(fieldInfo.Number, out var existingField))
                {
                    fieldNumberToInfo.Add(fieldInfo.Number, fieldInfo);
                    message.Fields.Add(fieldInfo);
                }
                else
                {
                    if (existingField.Name != fieldInfo.Name)
                    {
                        throw new InvalidOperationException($"Field number {fieldInfo.Number} is used by both {existingField.Name} and {fieldInfo.Name} on message {message.Type.FullName}.");
                    }
                }
            }
        }
    }

    private static bool HasOptionalProperty(TypeDefinition type, string name)
    {
        return type.Properties.Any(p =>
            p.Name == $"Has{name}"
            && p.PropertyType.FullName == "System.Boolean"
            && p.SetMethod is null
            && p.GetMethod?.HasThis == true);
    }

    private static PropertyDefinition? GetProperty(RebuilderContext ctx, TypeDefinition type, string name)
    {
        foreach (var property in type.Properties)
        {
            if (property.Name != name)
            {
                continue;
            }

            if (property.GetMethod?.HasThis != true)
            {
                continue;
            }

            // there is a lot of variety in when setters are present, so we ignore that information for now

            return property;
        }

        return null;
    }
}
