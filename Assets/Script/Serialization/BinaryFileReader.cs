using YARG.Types;
using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Unity.Collections.LowLevel.Unsafe;

namespace YARG.Serialization
{
    public enum Endianness
    {
        LittleEndian = 0,
        BigEndian = 1,
    };

#nullable enable
    public unsafe class BinaryFileReader : IDisposable
    {
        private readonly FrameworkFile? file;
        private readonly byte* ptr;
        private readonly int length;

        private int boundaryIndex = 0;
        private readonly int* boundaries;
        private int currentBoundary;

        private int _position;
        private bool disposedValue;

        public int Position
        {
            get { return _position; }
            set
            {
                if (value > boundaries[boundaryIndex])
                    throw new ArgumentOutOfRangeException("Position");
                _position = value;
            }
        }
        public int Boundary { get { return currentBoundary; } }

        public BinaryFileReader(byte* ptr, int length)
        {
            this.ptr = ptr;
            this.length = length;
            boundaries = (int*) Marshal.AllocHGlobal(sizeof(int) * 8);
            currentBoundary = boundaries[0] = length;
        }

        public BinaryFileReader(FrameworkFile file) : this(file.Ptr, file.Length)
        {
            this.file = file;
        }

        public BinaryFileReader(byte[] data) : this(new FrameworkFile_Handle(data)) { } 

        public BinaryFileReader(string path) : this(new FrameworkFile_Alloc(path)) { }

        public BinaryFileReader(PointerHandler handler, bool dispose = false) : this(new FrameworkFile_Pointer(handler, dispose)) { }

        protected void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing && file != null)
                    file.Dispose();
                Marshal.FreeHGlobal((IntPtr)boundaries);
                disposedValue = true;
            }
        }

        ~BinaryFileReader()
        {
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public void ExitSection()
        {
            _position = currentBoundary;
            if (boundaryIndex == 0)
                throw new Exception("ayo wtf bro");
            currentBoundary = boundaries[--boundaryIndex];
        }

        public void EnterSection(int length)
        {
            int boundary = _position + length;
            if (boundary > boundaries[boundaryIndex])
                throw new Exception("Invalid length for section");
            if (boundaryIndex == 7)
                throw new Exception("Nested Buffer limit reached");
            currentBoundary = boundaries[++boundaryIndex] = boundary;
        }

        public bool CompareTag(byte[] tag)
        {
            Debug.Assert(tag.Length == 4);
            if (tag[0] != ptr[_position] ||
                tag[1] != ptr[_position + 1] ||
                tag[2] != ptr[_position + 2] ||
                tag[3] != ptr[_position + 3])
                return false;

            _position += 4;
            return true;
        }

        public bool Move(int amount)
        {
            if (_position + amount > currentBoundary)
                return false;

            _position += amount;
            return true;
        }

        public void Move_Unsafe(int amount)
        {
            _position += amount;
        }

        public byte PeekByte()
        {
            return ptr[_position];
        }

        public bool ReadByte(ref byte value)
        {
            if (_position >= currentBoundary)
                return false;

            value = ptr[_position++];
            return true;
        }

        public bool ReadSByte(ref sbyte value)
        {
            if (_position >= currentBoundary)
                return false;

            value = (sbyte)ptr[_position++];
            return true;
        }
        
        public bool ReadInt16(ref short value, Endianness endianness = Endianness.LittleEndian)
        {
            if (_position + 2 > currentBoundary)
                return false;

            Span<byte> span = new(ptr + _position, 2);
            if (endianness == Endianness.LittleEndian)
                value = BinaryPrimitives.ReadInt16LittleEndian(span);
            else
                value = BinaryPrimitives.ReadInt16BigEndian(span);
            _position += 2;
            return true;
        }
        
        public bool ReadUInt16(ref ushort value, Endianness endianness = Endianness.LittleEndian)
        {
            if (_position + 2 > currentBoundary)
                return false;

            Span<byte> span = new(ptr + _position, 2);
            if (endianness == Endianness.LittleEndian)
                value = BinaryPrimitives.ReadUInt16LittleEndian(span);
            else
                value = BinaryPrimitives.ReadUInt16BigEndian(span);
            _position += 2;
            return true;
        }
        
        public bool ReadInt32(ref int value, Endianness endianness = Endianness.LittleEndian)
        {
            if (_position + 4 > currentBoundary)
                return false;

            Span<byte> span = new(ptr + _position, 4);
            if (endianness == Endianness.LittleEndian)
                value = BinaryPrimitives.ReadInt32LittleEndian(span);
            else
                value = BinaryPrimitives.ReadInt32BigEndian(span);
            _position += 4;
            return true;
        }
        
        public bool ReadUInt32(ref uint value, Endianness endianness = Endianness.LittleEndian)
        {
            if (_position + 4 > currentBoundary)
                return false;

            Span<byte> span = new(ptr + _position, 4);
            if (endianness == Endianness.LittleEndian)
                value = BinaryPrimitives.ReadUInt32LittleEndian(span);
            else
                value = BinaryPrimitives.ReadUInt32BigEndian(span);
            _position += 4;
            return true;
        }
        
        public bool ReadInt64(ref long value, Endianness endianness = Endianness.LittleEndian)
        {
            if (_position + 8 > currentBoundary)
                return false;

            Span<byte> span = new(ptr + _position, 8);
            if (endianness == Endianness.LittleEndian)
                value = BinaryPrimitives.ReadInt64LittleEndian(span);
            else
                value = BinaryPrimitives.ReadInt64BigEndian(span);
            Position += 8;
            return true;
        }
        
        public bool ReadUInt64(ref ulong value, Endianness endianness = Endianness.LittleEndian)
        {
            if (_position + 8 > currentBoundary)
                return false;

            Span<byte> span = new(ptr + _position, 8);
            if (endianness == Endianness.LittleEndian)
                value = BinaryPrimitives.ReadUInt64LittleEndian(span);
            else
                value = BinaryPrimitives.ReadUInt64BigEndian(span);
            _position += 8;
            return true;
        }

        public bool ReadFloat(ref float value)
        {
            if (_position + 4 > currentBoundary)
                return false;

            value = BitConverter.ToSingle(new Span<byte>(ptr + _position, 4));
            _position += 4;
            return true;
        }
        public byte ReadByte()
        {
            if (_position >= currentBoundary)
                throw new Exception("Failed to parse data");

            return ptr[_position++];
        }

        public ref byte ReadByte_Ref()
        {
            if (_position >= currentBoundary)
                throw new Exception("Failed to parse data");
            return ref ptr[_position++];
        }

        public sbyte ReadSByte()
        {
            if (_position >= currentBoundary)
                throw new Exception("Failed to parse data");

            return (sbyte)ptr[_position++];
        }

        public bool ReadBoolean()
        {
            return ReadByte() > 0;
        }
        public short ReadInt16(Endianness endianness = Endianness.LittleEndian)
        {
            short value = default;
            if (!ReadInt16(ref value, endianness))
                throw new Exception("Failed to parse data");
            return value;
        }
        public ushort ReadUInt16(Endianness endianness = Endianness.LittleEndian)
        {
            ushort value = default;
            if (!ReadUInt16(ref value, endianness))
                throw new Exception("Failed to parse data");
            return value;
        }
        public int ReadInt32(Endianness endianness = Endianness.LittleEndian)
        {
            int value = default;
            if (!ReadInt32(ref value, endianness))
                throw new Exception("Failed to parse data");
            return value;
        }
        public uint ReadUInt32(Endianness endianness = Endianness.LittleEndian)
        {
            uint value = default;
            if (!ReadUInt32(ref value, endianness))
                throw new Exception("Failed to parse data");
            return value;
        }
        public long ReadInt64(Endianness endianness = Endianness.LittleEndian)
        {
            long value = default;
            if (!ReadInt64(ref value, endianness))
                throw new Exception("Failed to parse data");
            return value;
        }
        public ulong ReadUInt64(Endianness endianness = Endianness.LittleEndian)
        {
            ulong value = default;
            if (!ReadUInt64(ref value, endianness))
                throw new Exception("Failed to parse data");
            return value;
        }
        public float ReadFloat()
        {
            float value = default;
            if (!ReadFloat(ref value))
                throw new Exception("Failed to parse data");
            return value;
        }
        public bool ReadBytes(byte[] bytes)
        {
            int endPos = _position + bytes.Length;
            if (endPos > currentBoundary)
                return false;

            Marshal.Copy((IntPtr)(ptr + _position), bytes, 0, bytes.Length);
            _position = endPos;
            return true;
        }

        public byte[] ReadBytes(int length)
        {
            byte[] bytes = new byte[length];
            if (!ReadBytes(bytes))
                throw new Exception("Failed to parse data");
            return bytes;
        }

        public string ReadLEBString()
        {
            int length = ReadLEB();
            return length > 0 ? Encoding.UTF8.GetString(ReadSpan(length)) : string.Empty;
        }

        public int ReadLEB()
        {
            uint result = 0;
            byte byteReadJustNow;

            const int MaxBytesWithoutOverflow = 4;
            for (int shift = 0; shift < MaxBytesWithoutOverflow * 7; shift += 7)
            {
                if (_position >= currentBoundary)
                    throw new Exception("Failed to parse data");

                byteReadJustNow = ptr[_position++];
                result |= (byteReadJustNow & 0x7Fu) << shift;

                if (byteReadJustNow <= 0x7Fu)
                {
                    return (int)result;
                }
            }

            if (_position >= currentBoundary)
                throw new Exception("Failed to parse data");

            byteReadJustNow = ptr[_position++];
            if (byteReadJustNow > 0b_1111u)
            {
                throw new Exception("Failed to parse data");
            }

            result |= (uint)byteReadJustNow << (MaxBytesWithoutOverflow * 7);
            return (int)result;
        }

        public uint ReadVLQ()
        {
            uint value = 0;
            uint i = 0;
            while (true)
            {
                if (_position >= currentBoundary)
                    throw new Exception("Failed to parse data");

                uint b = ptr[_position++];
                value |= b & 127;
                if (b < 128)
                    return value;

                if (i == 3)
                    throw new Exception("Invalid variable length quantity");

                value <<= 7;
                ++i;
            }     
        }

        public void CopyTo(byte* data, int length)
        {
            int endPos = _position + length;
            if (endPos > currentBoundary)
                throw new Exception("Failed to copy data");

            UnsafeUtility.MemCpy(data, ptr + _position, length);
            _position = endPos;
        }

        public ReadOnlySpan<byte> ReadSpan(int length)
        {
            int endPos = _position + length;
            if (endPos > currentBoundary)
                throw new Exception("Failed to parse data");

            ReadOnlySpan<byte> span = new(ptr + _position, length);
            _position = endPos;
            return span;
        }

        public BinaryFileReader CreateReaderFromCurrentPosition(int length)
        {
            int endPos = _position + length;
            if (endPos > currentBoundary)
                throw new Exception("Failed to create reader");

            BinaryFileReader reader = new(new FrameworkFile(ptr + _position, length));
            _position = endPos;
            return reader;
        }
    }
}
