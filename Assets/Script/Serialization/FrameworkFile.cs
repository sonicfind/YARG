﻿using YARG.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace YARG.Serialization
{
    public unsafe class FrameworkFile : IDisposable
    {
        public byte* ptr;
        protected bool disposedValue;

        public int Length { get; set; }

        protected FrameworkFile() { }

        public FrameworkFile(byte* ptr, int length)
        {
            this.ptr = ptr;
            Length = length;
        }

        public Hash128 CalcHash128() { return Hash128.Compute(ptr, (ulong) Length); }

        protected virtual void Dispose(bool disposing) { }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

    public unsafe class FrameworkFile_Handle : FrameworkFile
    {
        private readonly byte[] buffer;
        private readonly GCHandle handle;

        public FrameworkFile_Handle(byte[] data)
        {
            buffer = data;
            handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            ptr = (byte*)handle.AddrOfPinnedObject();
            Length = data.Length;
        }

        ~FrameworkFile_Handle() { Dispose(false); }

        protected override void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                handle.Free();
                disposedValue = true;
            }
        }
    }

    public unsafe class FrameworkFile_Alloc : FrameworkFile
    {
        public FrameworkFile_Alloc(string path)
        {
            using var fs = File.OpenRead(path);
            int length = (int)fs.Length;
            Length = length;
            ptr = (byte*)Marshal.AllocHGlobal(length);
            fs.Read(new Span<byte>(ptr, length));
        }

        ~FrameworkFile_Alloc() { Dispose(false); }

        protected override void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                Marshal.FreeHGlobal((IntPtr)ptr);
                disposedValue = true;
            }
        }
    }

    public unsafe class FrameworkFile_Pointer : FrameworkFile
    {
        private readonly PointerHandler handler;
        public FrameworkFile_Pointer(PointerHandler handler, bool dispose = false)
        {
            this.handler = handler;
            ptr = handler.Data;
            Length = handler.Length;
            disposedValue= !dispose;
        }

        protected override void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                handler.Dispose();
                disposedValue = true;
            }
        }
    }
}
