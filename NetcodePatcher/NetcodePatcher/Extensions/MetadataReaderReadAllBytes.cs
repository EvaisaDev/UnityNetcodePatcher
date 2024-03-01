#nullable enable

using System;
using System.Reflection.Metadata;

namespace NetcodePatcher.Extensions;

public static class MetadataReaderReadAllBytes
{
    public static unsafe byte[] ReadAllBytes(this MetadataReader reader)
    {
        var buffer = new byte[reader.MetadataLength];
        fixed (byte* bufferPtr = &buffer[0])
        {
            Buffer.MemoryCopy(reader.MetadataPointer, bufferPtr, buffer.Length, buffer.Length);
        }

        return buffer;
    }
}
