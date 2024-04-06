﻿using System.Runtime.InteropServices;
using System.Text;

namespace ReplayPlugin.Utils;

public static class BinaryWriterExtensions
{
    public static void WriteStruct<T>(this BinaryWriter writer, T value) where T : unmanaged
    {
        writer.Write(MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref value, 1)));
    }

    public static void WriteACString(this BinaryWriter writer, string? str)
    {
        str ??= "";
        writer.Write((uint)str.Length);
        writer.Write(Encoding.UTF8.GetBytes(str));
    }
}
