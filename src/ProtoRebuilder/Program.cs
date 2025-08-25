using Microsoft.Extensions.Logging;
using Mono.Cecil;

namespace Knapcode.ProtoRebuilder;

public class Program
{
    public static int Main(string[] args)
    {
        var logger = new ConsoleLogger();
        try
        {
            return Execute(args, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error: {Message}", ex.Message);
            logger.LogError(ex.StackTrace);
            return 1;
        }
    }

    public static int Execute(string[] args, ILogger logger)
    {
        if (args.Length < 2
            || args.Contains("-h", StringComparer.OrdinalIgnoreCase)
            || args.Contains("--help", StringComparer.OrdinalIgnoreCase)
            || args.Contains("/?", StringComparer.OrdinalIgnoreCase))
        {
            ShowUsage(logger);
            return 1;
        }

        string assemblyPath = Path.GetFullPath(args[0]);
        if (!File.Exists(assemblyPath))
        {
            logger.LogError("Assembly not found: {assemblyPath}", assemblyPath);
            return 1;
        }

        logger.LogInformation("Analyzing assembly: {assemblyPath}", assemblyPath);

        var assemblyDir = Path.GetDirectoryName(assemblyPath)!;
        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(assemblyDir);

        var readerParameters = new ReaderParameters { AssemblyResolver = resolver };
        var targetAssembly = AssemblyDefinition.ReadAssembly(assemblyPath, readerParameters);

        var protobufReference = targetAssembly.MainModule.AssemblyReferences.FirstOrDefault(ar => ar.Name == "Google.Protobuf");
        if (protobufReference == null)
        {
            logger.LogError("Google.Protobuf assembly reference not found in the target assembly.");
            return 1;
        }

        logger.LogInformation("Found Google.Protobuf {Version} reference.", protobufReference.Version);

        var protobufAssembly = resolver.Resolve(protobufReference);
        RebuilderContext ctx = InitializeContext(protobufAssembly, logger);

        var exitCode = AnalyzeAssembly(ctx, targetAssembly);
        if (exitCode != 0)
        {
            return exitCode;
        }

        logger.LogInformation("");
        logger.LogInformation("Generating .proto files...");
        var protoFiles = ProtoFileGenerator.GenerateProtoFiles(ctx);
        logger.LogInformation("{Count} .proto files will be written:", protoFiles.Count);
        foreach (var protoFile in protoFiles)
        {
            logger.LogInformation("  {FileName}", protoFile.FileName);
            logger.LogInformation(
                "    package: {PackageName}, namespace: {Namespace}, messages: {MessageCount}, enums {EnumCount}",
                protoFile.PackageName,
                protoFile.CsharpNamespace,
                protoFile.MessageTypes.Count,
                protoFile.EnumTypes.Count);
        }

        logger.LogInformation("");
        var directory = Path.GetFullPath(args[1]);
        logger.LogInformation("Writing .proto files to {Directory}...", directory);
        if (!Directory.Exists(directory))
        {
            logger.LogWarning("Directory does not exist, creating: {Directory}", directory);
            Directory.CreateDirectory(directory);
        }

        foreach (var protoFile in protoFiles)
        {
            var path = Path.Combine(args[1], protoFile.FileName);
            logger.LogInformation("Writing {Path}...", path);
            using var fileStream = new FileStream(path, FileMode.Create);
            using var writer = new StreamWriter(fileStream);
            ProtoFileWriter.WriteProtoSchema(ctx, protoFile, writer);
        }

        logger.LogInformation("");
        logger.LogInformation("Done.");

        return 0;
    }

    private static int AnalyzeAssembly(RebuilderContext ctx, AssemblyDefinition targetAssembly)
    {
        ctx.Logger.LogInformation("");
        ctx.Logger.LogInformation("Gathering message and enum types from the assembly...");
        TypeAnalyzer.GatherRelevantTypes(ctx, targetAssembly);
        TypeAnalyzer.PopulateMessageFields(ctx);

        ctx.Logger.LogInformation("Found {MessageTypeCount} message types.", ctx.MessageTypes.Count);
        ctx.Logger.LogInformation("Found {EnumTypeCount} enum types.", ctx.EnumTypes.Count);
        
        if (ctx.MessageTypes.Count == 0)
        {
            ctx.Logger.LogWarning("No message types found in the assembly.");
        }

        ctx.Logger.LogInformation("Message type namespaces: ");
        var namespaces = ctx.MessageTypes
            .Select(m => m.Type.Namespace)
            .Distinct()
            .OrderBy(ns => ns, StringComparer.Ordinal)
            .ToList();
        foreach (var ns in namespaces)
        {
            ctx.Logger.LogInformation("  '{Namespace}'", ns);
        }

        var enumCount = ctx.EnumTypes.Count;

        ctx.Logger.LogInformation("");
        ctx.Logger.LogInformation("Analyzing nested types and message dependencies...");
        LogicalAnalyzer.ConnectChildTypes(ctx);
        LogicalAnalyzer.RemoveUnreferencedEnums(ctx);
        LogicalAnalyzer.PopulateMessageDependencies(ctx);

        ctx.Logger.LogInformation("Pruned {PrunedEnumCount} enums not referenced by any message.", enumCount - ctx.EnumTypes.Count);

        ctx.Logger.LogInformation("");
        ctx.Logger.LogInformation("Grouping messages and enums into modules...");
        ModuleGenerator.GatherEnumModules(ctx);
        ModuleGenerator.GatherMessageModules(ctx);
        ctx.Logger.LogInformation("Adding module dependencies...");
        ModuleGenerator.AddModuleDependencies(ctx);
        ctx.Logger.LogInformation("Initialized {ModuleCount} modules with {DependencyEdgeCount} dependency edges.", ctx.Modules.Count, ctx.Modules.Sum(x => x.DependsOn.Count));

        ModuleMerger.MergeModules(ctx);

        return 0;
    }

    private static void ShowUsage(ILogger logger)
    {
        logger.LogInformation(
            """
            Usage: ProtoRebuilder ASSEMBLY_PATH OUTPUT_DIR

            Rebuilds .proto files from an assembly containing Google.Protobuf messages.

              ASSEMBLY_PATH: path to a .NET assembly with a Google.Protobuf reference and IMessage implementations
              OUTPUT_DIR:    directory where the generated .proto files will be saved
            """);
    }

    private static RebuilderContext InitializeContext(AssemblyDefinition protobufAssembly, ILogger logger)
    {
        var messageType = protobufAssembly.MainModule.GetType("Google.Protobuf.IMessage").Resolve();
        var fileDescriptorType = protobufAssembly.MainModule.GetType("Google.Protobuf.Reflection.FileDescriptor").Resolve();
        var repeatedFieldType = protobufAssembly.MainModule.GetType("Google.Protobuf.Collections.RepeatedField`1").Resolve();
        var mapFieldType = protobufAssembly.MainModule.GetType("Google.Protobuf.Collections.MapField`2").Resolve();

        var ctx = new RebuilderContext
        {
            Logger = logger,
            MessageInterface = messageType,
            FileDescriptorType = fileDescriptorType,
            RepeatedFieldType = repeatedFieldType,
            MapFieldType = mapFieldType,
        };
        return ctx;
    }
}
