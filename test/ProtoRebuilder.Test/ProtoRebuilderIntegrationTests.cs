using System.Runtime.InteropServices;
using CliWrap;
using EmptyFiles;
using Xunit.Abstractions;

namespace Knapcode.ProtoRebuilder.Test;

public class ProtoRebuilderIntegrationTests
{
    static ProtoRebuilderIntegrationTests()
    {
        FileExtensions.AddTextExtension("proto");
    }

    public ITestOutputHelper Output { get; }

    public ProtoRebuilderIntegrationTests(ITestOutputHelper output)
    {
        Output = output;
    }

    public static IEnumerable<object[]> GetProtoTestNames()
    {
        var baseDir = Path.Combine(Directory.GetCurrentDirectory(), "protos");
        foreach (var dir in Directory.GetDirectories(baseDir))
        {
            var testName = Path.GetFileName(dir)!;
            yield return new object[] { testName };
        }
    }

    [Theory]
    [MemberData(nameof(GetProtoTestNames))]
    public async Task CanCompileAndRebuildProtos(string testName)
    {
        // Arrange
        var toolsPath = Path.Combine(Directory.GetCurrentDirectory(), "tools.txt");
        var tools = File
            .ReadAllText(toolsPath)
            .Split(['\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.Split(['='], 2))
            .ToDictionary(x => x[0].Trim(), x => Path.GetFullPath(x[1].Trim()));

        var packageBase = tools["PkgGrpc_Tools"];
        var protocImports = Path.Combine(packageBase, "build", "native", "include");
        string protocPath = GetProtocPath(packageBase);

        var outDir = Path.Combine(Directory.GetCurrentDirectory(), "ProtoRebuilderTest", Path.GetFileName(testName));
        if (Directory.Exists(outDir))
        {
            Directory.Delete(outDir, recursive: true);
        }
        Directory.CreateDirectory(outDir);

        // Find all .proto files in the directory
        var fullProtoDir = Path.Combine(Directory.GetCurrentDirectory(), "Protos", testName);
        var protoFiles = Directory.GetFiles(fullProtoDir, "*.input.proto", SearchOption.TopDirectoryOnly);
        if (protoFiles.Length == 0)
            throw new InvalidOperationException($"No .proto files found in {testName}");

        await ExecuteAsync(Cli
            .Wrap(protocPath)
            .WithArguments(new string[] { $"-I{protocImports}", "--csharp_out", outDir, "--proto_path", fullProtoDir }.Concat(protoFiles)));

        await ExecuteAsync(Cli
            .Wrap("dotnet")
            .WithWorkingDirectory(outDir)
            .WithArguments(["new", "console"]));

        await ExecuteAsync(Cli
            .Wrap("dotnet")
            .WithWorkingDirectory(outDir)
            .WithArguments(["package", "add", "Google.Protobuf"]));

        await ExecuteAsync(Cli
            .Wrap("dotnet")
            .WithWorkingDirectory(outDir)
            .WithArguments(["build"]));

        var assembly = Directory
            .GetFiles(Path.Combine(outDir, "bin"), $"{testName}.dll", SearchOption.AllDirectories)
            .Single();

        var logger = new TestOutputLogger(Output);
        var outputProtoDir = Path.Combine(outDir, "proto");
        var exitCode = Program.Execute([assembly, outputProtoDir], logger);
        Assert.Equal(0, exitCode);

        var outputProtoFiles = Directory
            .GetFiles(outputProtoDir, "*.proto", SearchOption.TopDirectoryOnly)
            .Select(x => Path.GetRelativePath(outputProtoDir, x).Replace("\\", "/"))
            .Order(StringComparer.Ordinal)
            .Select(x => (Path: x, Content: File.ReadAllText(Path.Combine(outputProtoDir, x))))
            .Select(x => new Target(extension: "proto", data: x.Content, name: Path.GetFileNameWithoutExtension(x.Path)))
            .ToList();

        await Verify(outputProtoFiles)
            .UseDirectory(Path.Combine("protos", testName))
            .UseFileName("output");
    }

    private async Task ExecuteAsync(Command builder)
    {
        Output.WriteLine(new string('-', 40));
        Output.WriteLine($"Executing command:");
        Output.WriteLine($"{builder.TargetFilePath} {builder.Arguments}");

        try
        {
            Output.WriteLine("");
            await builder
                .WithStandardErrorPipe(PipeTarget.ToDelegate(Output.WriteLine))
                .WithStandardOutputPipe(PipeTarget.ToDelegate(Output.WriteLine))
                .ExecuteAsync();
        }
        finally
        {
            Output.WriteLine("");
            Output.WriteLine(new string('-', 40));
        }
    }

    private static string GetProtocPath(string packageBase)
    {
        var os = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows" :
                 RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "linux" :
                 RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "macosx" :
                 throw new PlatformNotSupportedException("Unsupported OS");
        var arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            Architecture.X86 => "x86",
            _ => throw new PlatformNotSupportedException("Unsupported architecture")
        };
        var extension = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : string.Empty;
        var protoc = Path.Combine(packageBase, "tools", $"{os}_{arch}", $"protoc{extension}");
        return protoc;
    }
}