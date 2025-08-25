# ProtoRebuilder

Generate .proto files from .NET assemblies.

This is a .NET tool that scans .NET assemblies for types that implement `IMessage` and generates corresponding `.proto` files.


The idea is that you used [protoc](https://github.com/protocolbuffers/protobuf/releases) (either directly or via [Grpc.Tools](https://www.nuget.org/packages/Grpc.Tools)) to generate some C# from .proto files. This was compiled into an assembly. Then you lost the .proto files! What do you do? You can generate .proto files from the assembly using this tool.

Maybe in the future, I will enhance the tool to also generate .proto files from any group of .NET types (perhaps as a way to transition from one wire format to another), but for now, it only works with assemblies containing `Google.Protobuf.IMessage` types.

## Usage

Install the .NET SDK from https://get.dot.net/. This allows you to run .NET tool packages easily.

With .NET SDK 10+, you can use `dnx`:

```
dnx Knapcode.ProtoRebuilder path/to/assembly.dll dir/for/protos
```

With .NET SDK 8+, you can use `dotnet tool install`:
```
dotnet tool install --global Knapcode.ProtoRebuilder
```

Then run the tool with:
```
protorebuilder path/to/assembly.dll dir/for/protos
```

This will scan the assembly (first argument) for types that implement `Google.Protobuf.IMessage` and generate `.proto` files in the specified directory (second argument). If the `IMessage` types are in different namespaces, multiple proto files will be generated, one for each namespace. Only enums used by the `IMessage` types will be included in the generated `.proto` files.

Take a look at the test data for examples of the input and output `.proto` files. Here is a sample:

- Input: [`test/ProtoRebuilder.Test/protos/Sample`](https://github.com/joelverhagen/ProtoRebuilder/blob/main/test/ProtoRebuilder.Test/protos/Simple/simple.input.proto)
- Output: [`test/ProtoRebuilder.Test/protos/Sample`](https://github.com/joelverhagen/ProtoRebuilder/blob/main/test/ProtoRebuilder.Test/protos/Simple/output%23test.simple.verified.proto)

## Caveats

This is not a perfect round-trip (proto -> .NET assembly -> proto). Here is a list of problems I know about:

- Enum names may change. The values (which are what go over the wire) should stay the same.
- The well-known type [`StringValue`](https://protobuf.dev/reference/protobuf/google.protobuf/#string-value) is assumed to be a `string` (no wrapper). This can cause deserialization issues because the string value is actually wrapped in a message with a single `value` field, but the generated .proto files only know about the inner string (no wrapper).