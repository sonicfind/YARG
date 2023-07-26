using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;

namespace YARG.Serialization
{
    public class XboxImageSettings
    {
        public readonly byte bitsPerPixel;
        public readonly int format;
        public readonly int width;
        public readonly int height;
        public unsafe XboxImageSettings(byte* data)
        {
            unsafe
            {
                bitsPerPixel = data[1];
                format = BinaryPrimitives.ReadInt32LittleEndian(new(data + 2, 4));
                width = BinaryPrimitives.ReadInt16LittleEndian(new(data + 7, 2));
                height = BinaryPrimitives.ReadInt16LittleEndian(new(data + 9, 2));
            }
        }
    }

    public static class XboxImageTextureGenerator
    {
#nullable enable
        public static unsafe XboxImageSettings? GetTexture(FrameworkFile file, CancellationToken ct)
        {
            // Swap bytes because xbox is weird like that
            byte* data = file.Ptr;
            int length = file.Length;
            byte buf;
            for (int i = 32; i < length; i += 2)
            {
                buf = data[i];
                data[i] = data[i + 1];
                data[i + 1] = buf;
            }

            if (ct.IsCancellationRequested)
                return null;
            return new XboxImageSettings(data);
        }
    }
}