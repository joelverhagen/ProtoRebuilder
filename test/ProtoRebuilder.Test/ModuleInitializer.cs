using System.Runtime.CompilerServices;
using EmptyFiles;

namespace Knapcode.ProtoRebuilder.Test;

public static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Init()
    {
        FileExtensions.AddTextExtension("proto");
    }
}
