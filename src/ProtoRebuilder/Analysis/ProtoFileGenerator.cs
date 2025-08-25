namespace Knapcode.ProtoRebuilder;

public static class ProtoFileGenerator
{
    public static IReadOnlyList<ProtoFile> GenerateProtoFiles(RebuilderContext ctx)
    {
        var fileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var moduleToProtoFile = new Dictionary<ProtoModule, ProtoFile>();

        var sortedModules = ctx
            .Modules
            .OrderByDescending(x => x.DependsOn.Count == 0)
            .ThenBy(x => x.RootMessages.Count)
            .ThenBy(x => x.Id);

        foreach (var module in sortedModules)
        {
            var (packageName, fileNameBase) = GetPackageNameAndFileNameBase(module.Namespace);
            var multipleFilesInNamespace = ctx.Modules.Count(x => x.Namespace == module.Namespace) > 1;
            string fileName;
            if (multipleFilesInNamespace)
            {
                var index = 1;
                while (true)
                {
                    fileName = $"{fileNameBase}.{index}.proto";
                    if (!fileNames.Contains(fileName))
                    {
                        break;
                    }

                    index++;
                }
            }
            else
            {
                fileName = $"{fileNameBase}.proto";
            }

            if (!fileNames.Add(fileName))
            {
                throw new InvalidCastException($"File name {fileName} was already used.");
            }

            var imports = new HashSet<string>();
            foreach (var message in module.RootMessages)
            {
                imports.UnionWith(message.Imports);
            }

            moduleToProtoFile.Add(module, new ProtoFile
            {
                FileName = fileName,
                PackageName = packageName,
                Imports = imports.Order(StringComparer.Ordinal).ToList(),
                CsharpNamespace = module.Namespace,
                EnumTypes = module.RootEnums.OrderBy(x => x.Type.Name).ToList(),
                MessageTypes = module.RootMessages.OrderBy(x => x.Type.Name).ToList(),
            });
        }

        foreach (var (module, protoFile) in moduleToProtoFile)
        {
            foreach (var dependency in module.DependsOn)
            {
                protoFile.Imports.Add(moduleToProtoFile[dependency].FileName);
            }
        }

        return moduleToProtoFile
            .Values
            .OrderBy(x => x.FileName, StringComparer.Ordinal)
            .ToList();
    }

    private static (string PackageName, string FileNameBase) GetPackageNameAndFileNameBase(string ns)
    {
        var packageName = ProtoTypeMapper.PascalToLowerSnakeCase(ns);
        var fileNameBase = packageName.Length == 0 ? "base" : packageName;
        return (packageName, fileNameBase);
    }
}
