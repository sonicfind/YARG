﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace YARG.Types
{
    public unsafe class PointerHandler : IDisposable
    {
        private byte* data = null;
        private bool disposedValue;
        private int length;

        public PointerHandler(int length)
        {
            this.length = length;
            data = (byte*)Marshal.AllocHGlobal(length);
        }

        public PointerHandler(byte* ptr, int length)
        {
            this.length = length;
            data = (byte*)Marshal.AllocHGlobal(length);
            Copier.MemCpy(data, ptr, (nuint)length);
        }

        public PointerHandler(PointerHandler handler)
        {
            length = handler.length;
            data = (byte*)Marshal.AllocHGlobal(length);
            Copier.MemCpy(data, handler.data, (nuint)length);
        }

        public byte* Data => data;
        public int Length => length;

        public ReadOnlySpan<byte> AsReadOnlySpan() { return new(data, length); }
        public Span<byte> AsSpan() { return new(data, length); }

        public Hash128 CalcHash128() { return Hash128.Compute(data, (ulong) Length); }

        public byte* Release()
        {
            byte* ptr = data;
            data = null;
            return ptr;
        }

        public void Append(byte* ptr, int length)
        {
            int newLength = this.length + length;
            byte* newData = (byte*)Marshal.AllocHGlobal(newLength);
            unsafe
            {
                Copier.MemCpy(newData, data, (nuint)this.length);
                Copier.MemCpy(newData + this.length, ptr, (nuint)length);
            }
            Marshal.FreeHGlobal((IntPtr)data);
            this.length = newLength;
            data = newData;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                Marshal.FreeHGlobal((IntPtr)data);
                disposedValue = true;
            }
        }

        ~PointerHandler()
        {
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}