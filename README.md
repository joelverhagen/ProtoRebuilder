# ProtoRebuilder

Generate .proto files from .NET assemblies.

This is a .NET tool that scans .NET assemblies for types that implement `IMessage` and generates corresponding `.proto` files.

The idea is that you used [protoc](https://github.com/protocolbuffers/protobuf/releases) (either directly or via [Grpc.Tools](https://www.nuget.org/packages/Grpc.Tools) to generate some C# from .proto files. This was compiled into an assembly. Then you lost the .proto files! What do you do? You can generate .proto files from the assembly using this tool.

Maybe in the future I will enhance the tool to also generate .proto files from any .NET types (perhaps as a way to transition from one wire format to another), but for now it only works with assemblies containing `Google.Protobuf.IMessage` types.
