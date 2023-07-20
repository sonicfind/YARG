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
        public XboxImageSettings(FrameworkFile xboxImage)
        {
            unsafe
            {
                bitsPerPixel = xboxImage.ptr[1];
                format = BinaryPrimitives.ReadInt32LittleEndian(new(xboxImage.ptr + 2, 4));
                width = BinaryPrimitives.ReadInt16LittleEndian(new(xboxImage.ptr + 7, 2));
                height = BinaryPrimitives.ReadInt16LittleEndian(new(xboxImage.ptr + 9, 2));
            }
        }
    }

    public static class XboxImageTextureGenerator
    {
#nullable enable
        public static unsafe XboxImageSettings? GetTexture(FrameworkFile xboxImage, CancellationToken ct)
        {
            // Swap bytes because xbox is weird like that
            byte buf;
            for (int i = 32; i < xboxImage.Length; i += 2)
            {
                buf = xboxImage.ptr[i];
                xboxImage.ptr[i] = xboxImage.ptr[i + 1];
                xboxImage.ptr[i + 1] = buf;
            }

            if (ct.IsCancellationRequested)
                return null;
            return new XboxImageSettings(xboxImage);
        }
    }
}