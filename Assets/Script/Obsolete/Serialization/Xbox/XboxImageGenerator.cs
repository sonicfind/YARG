using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using YARG.Audio;

namespace YARG.Serialization
{
    public static class XboxImageTextureGenerator
    {
        public static unsafe async UniTask<Texture2D> GetTexture(FrameworkFile xboxImage, CancellationToken ct)
        {
            // Parse header and get DXT blocks
            byte BitsPerPixel = xboxImage.ptr[1];
            int Format = BinaryPrimitives.ReadInt32LittleEndian(new(xboxImage.ptr + 2, 4));
            short Width = BinaryPrimitives.ReadInt16LittleEndian(new(xboxImage.ptr + 7, 2));
            short Height = BinaryPrimitives.ReadInt16LittleEndian(new(xboxImage.ptr + 9, 2));
            bool isDXT1 = ((BitsPerPixel == 0x04) && (Format == 0x08));

            ct.ThrowIfCancellationRequested();

            // Swap bytes because xbox is weird like that
            byte buf;
            for (int i = 32; i < xboxImage.Length; i += 2)
            {
                buf = xboxImage.ptr[i];
                xboxImage.ptr[i] = xboxImage.ptr[i + 1];
                xboxImage.ptr[i + 1] = buf;
            }

            ct.ThrowIfCancellationRequested();

            // apply DXT1 OR DXT5 formatted bytes to a Texture2D
            var tex = new Texture2D(Width, Height,
                (isDXT1) ? GraphicsFormat.RGBA_DXT1_SRGB : GraphicsFormat.RGBA_DXT5_SRGB, TextureCreationFlags.None);
            tex.LoadRawTextureData((IntPtr)(xboxImage.ptr + 32), xboxImage.Length - 32);
            tex.Apply();

            return tex;
        }
    }
}